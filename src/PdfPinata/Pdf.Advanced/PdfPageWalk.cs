using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfPinata.Internal;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Reads a page's content and everything it draws — following forms, soft masks, Type 3 char procs
/// and annotation appearances, to a bounded depth — the way <see cref="PdfResourcePruner"/> has
/// always read a page to decide what it draws with.
/// <para>
/// This class only walks; it does not decide what the walk means. <see cref="PdfResourcePruner"/>
/// asks it which names in the page's own resource dictionary the page actually draws with, so a
/// dictionary shared by several pages can be pruned to what one of them uses.
/// <see cref="PdfPageResourceUsage"/> asks it which images, forms, colour spaces and graphics states
/// a page reaches at all, however deep the forms and soft masks nest, so the PDF/A rules that need a
/// page-resource walk can ask their own questions of the same answer. Neither subclass changes what
/// is walked, how far, or what "understood" means — a page whose content defeats the walk is left
/// unjudged by both, rather than judged on a guess.
/// </para>
/// </summary>
internal abstract class PdfPageWalk
{
    /// <summary>
    /// How deep forms may be drawn within one another before the walk gives up. Well past anything
    /// a real document does, and there to stop a malformed one running away.
    /// </summary>
    private const int MaximumDepth = 32;

    /// <summary>The colour spaces that are always available and never named by a resource dictionary.</summary>
    private static readonly string[] DeviceColorSpaces = ["/DeviceGray", "/DeviceRGB", "/DeviceCMYK", "/Pattern"];

    protected PdfPageWalk(PdfDictionary pageResources)
    {
        PageResources = pageResources;
    }

    /// <summary>The resource dictionary the page itself was given.</summary>
    protected PdfDictionary PageResources { get; }

    /// <summary>
    /// The streams already read, each paired with the scope it was read in, so that a form drawing
    /// itself does not go round forever.
    /// </summary>
    private readonly HashSet<StreamInScope> _read = [];

    /// <summary>Whether everything read so far was understood.</summary>
    protected bool _understood = true;

    /// <summary>
    /// Whether the whole page was read and followed without anything defeating the walk — an inline
    /// image, content that could not be decoded, or a soft mask reaching a form that could not be.
    /// </summary>
    internal bool Understood => _understood;

    #region Reading

    protected void ReadPage(PdfPage page)
    {
        if (!PdfContentStreams.TryGetPageContent(page, out var content))
        {
            _understood = false;
            return;
        }

        Read(content, PageResources, 0);

        // The appearance of an annotation is a form, and one without resources of its own falls
        // back on those of the page.
        ReadAppearances(page);
    }

    private void Read(byte[] content, PdfDictionary scope, int depth)
    {
        if (!_understood)
            return;

        if (depth > MaximumDepth)
        {
            _understood = false;
            return;
        }

        CSequence sequence;
        try
        {
            sequence = ContentReader.ReadContent(content);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // Content that cannot be read cannot be told what it draws with.
            _understood = false;
            return;
        }

        ReadSequence(sequence, scope, depth);
    }

    private void ReadSequence(CSequence sequence, PdfDictionary scope, int depth)
    {
        foreach (var item in sequence)
        {
            var op = item as COperator;
            if (op == null)
                continue;

            Observe(op, scope, depth);

            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.BI:
                    // The lexer finds the end of an inline image by looking for the bytes of EI,
                    // which the image data itself may hold. A wrong guess carries the reading off
                    // and the operators after it are lost, so nothing read after it can be trusted.
                    _understood = false;
                    return;

                case OpCodeName.Do:
                    Use("/XObject", NameAt(op, 0), scope, depth);
                    break;

                case OpCodeName.Tf:
                    Use("/Font", NameAt(op, 0), scope, depth);
                    break;

                case OpCodeName.gs:
                    Use("/ExtGState", NameAt(op, 0), scope, depth);
                    break;

                case OpCodeName.sh:
                    Use("/Shading", NameAt(op, 0), scope, depth);
                    break;

                case OpCodeName.cs:
                case OpCodeName.CS:
                    var colorSpace = NameAt(op, 0);
                    if (colorSpace != null && Array.IndexOf(DeviceColorSpaces, colorSpace) < 0)
                        Use("/ColorSpace", colorSpace, scope, depth);
                    break;

                case OpCodeName.scn:
                case OpCodeName.SCN:
                    // A pattern is named last, after the components of the underlying colour.
                    Use("/Pattern", NameAt(op, op.Operands.Count - 1), scope, depth);
                    break;

                case OpCodeName.BDC:
                case OpCodeName.DP:
                    // The property list is named second, after the tag, unless it is written out.
                    Use("/Properties", NameAt(op, 1), scope, depth);
                    break;
            }

            if (!_understood)
                return;
        }
    }

    /// <summary>
    /// Called for every operator read, before it is otherwise acted on. The only override is
    /// <see cref="PdfPageResourceUsage"/>'s, which is how it notices a device colour space set
    /// outright — <c>g</c>, <c>rg</c> and <c>k</c> and their stroking spellings paint with a colour
    /// space no resource dictionary ever names.
    /// </summary>
    protected virtual void Observe(COperator op, PdfDictionary scope, int depth)
    {
    }

    private static string NameAt(COperator op, int index)
    {
        if (index < 0 || index >= op.Operands.Count)
            return null;

        var name = op.Operands[index] as CName;
        return name?.Name;
    }

    #endregion

    #region Following what is drawn

    /// <summary>
    /// Records that the content in scope draws with the named resource, and reads whatever that
    /// resource draws in its turn.
    /// </summary>
    private void Use(string category, string name, PdfDictionary scope, int depth)
    {
        if (name == null)
            return;

        RecordUse(category, name, scope);

        var resolved = ResolveRaw(category, name, scope);
        RecordResolved(category, name, resolved);

        var resource = resolved as PdfDictionary;
        if (resource == null)
        {
            // The content names something the resources do not hold, or something that is not a
            // dictionary at all — a bare colour space name, say. There is nothing to follow.
            return;
        }

        switch (category)
        {
            case "/XObject":
                if (resource.Elements.GetName("/Subtype") == "/Form")
                    ReadNested(resource, resource, scope, depth);
                break;

            case "/Pattern":
                // A tiling pattern is drawn from a content stream; a shading pattern is not.
                if (resource.Elements.GetInteger("/PatternType") == 1)
                    ReadNested(resource, resource, scope, depth);
                break;

            case "/Font":
                if (resource.Elements.GetName("/Subtype") == "/Type3")
                    ReadCharProcs(resource, scope, depth);
                break;

            case "/ExtGState":
                UseSoftMask(resource, scope, depth);
                break;
        }
    }

    /// <summary>
    /// Called for every named use at the page's own scope — what a shared resource dictionary can be
    /// pruned down to. <see cref="PdfResourcePruner"/> is the only override.
    /// </summary>
    protected virtual void RecordUse(string category, string name, PdfDictionary scope)
    {
    }

    /// <summary>
    /// Called for every named use wherever it is reached, with whatever the name resolved to in that
    /// scope — a dictionary for most categories, but a colour space may resolve to an array or to a
    /// bare device name instead, and null when nothing in scope answers to the name.
    /// <see cref="PdfPageResourceUsage"/> is the only override.
    /// </summary>
    protected virtual void RecordResolved(string category, string name, PdfItem resolved)
    {
    }

    /// <summary>
    /// Follows the soft mask of a graphics state. The mask is painted by a form, and a form
    /// without resources of its own paints with those of whatever set the state, so what the
    /// mask names has to be read as well.
    /// </summary>
    private void UseSoftMask(PdfDictionary extGState, PdfDictionary scope, int depth)
    {
        var item = extGState.Elements["/SMask"];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item == null)
        {
            // The state says nothing about the mask and leaves it as it was.
            return;
        }

        if (item is PdfName none)
        {
            // A mask set to /None paints nothing. Any other name is not a mask this knows.
            if (none.Value != "/None")
                _understood = false;
            return;
        }

        var mask = item as PdfDictionary;
        var group = mask?.Elements.GetDictionary("/G");
        if (group == null)
        {
            // A mask whose form cannot be reached may paint with anything.
            _understood = false;
            return;
        }

        ReadNested(group, group, scope, depth);
    }

    /// <summary>
    /// Reads a stream drawn by the content, in the scope of its own resources where it has them
    /// and in the scope it was drawn from where it has not.
    /// </summary>
    private void ReadNested(PdfDictionary stream, PdfDictionary owningResources, PdfDictionary scope, int depth)
    {
        var nested = ScopeOf(owningResources, scope);

        if (!MarkAsRead(stream, nested))
            return;

        if (!TryGetContent(stream, out var content))
        {
            // A stream that cannot be read may draw with anything.
            _understood = false;
            return;
        }

        Read(content, nested, depth + 1);
    }

    private void ReadCharProcs(PdfDictionary font, PdfDictionary scope, int depth)
    {
        var charProcs = font.Elements.GetDictionary("/CharProcs");
        if (charProcs == null)
            return;

        var fontScope = ScopeOf(font, scope);
        foreach (var glyph in charProcs.Elements.KeyNames)
        {
            var procedure = charProcs.Elements.GetDictionary(glyph.Value);
            if (procedure == null)
                continue;

            if (!MarkAsRead(procedure, fontScope))
                continue;

            if (!TryGetContent(procedure, out var content))
            {
                _understood = false;
                return;
            }

            Read(content, fontScope, depth + 1);
            if (!_understood)
                return;
        }
    }

    private void ReadAppearances(PdfPage page)
    {
        var annotations = page.Elements.GetArray(PdfPage.Keys.Annots);
        if (annotations == null)
            return;

        for (var idx = 0; idx < annotations.Elements.Count && _understood; idx++)
        {
            var annotation = annotations.Elements.GetDictionary(idx);
            var appearance = annotation == null
                ? null
                : annotation.Elements.GetDictionary("/AP");
            if (appearance == null)
                continue;

            foreach (var kind in appearance.Elements.KeyNames)
            {
                var stream = appearance.Elements.GetDictionary(kind.Value);
                if (stream == null)
                    continue;

                if (stream.Stream != null)
                {
                    ReadNested(stream, stream, PageResources, 0);
                }
                else
                {
                    // An appearance that changes with the state of the annotation is a
                    // dictionary of one stream per state.
                    foreach (var state in stream.Elements.KeyNames)
                    {
                        var perState = stream.Elements.GetDictionary(state.Value);
                        if (perState is { Stream: not null })
                            ReadNested(perState, perState, PageResources, 0);
                    }
                }

                if (!_understood)
                    return;
            }
        }
    }

    /// <summary>
    /// The scope names in a stream resolve against: its own resources where it has them, and
    /// those it was drawn from where it has not.
    /// </summary>
    private static PdfDictionary ScopeOf(PdfDictionary owner, PdfDictionary scope)
    {
        return owner.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources) ?? scope;
    }

    /// <summary>
    /// The raw value a named resource resolves to, in scope — a dictionary for most categories, but
    /// a colour space may resolve to an array or a bare name instead. A dangling reference or a PDF
    /// null resolves to null, exactly as the specification says leaving the entry out would.
    /// </summary>
    private static PdfItem ResolveRaw(string category, string name, PdfDictionary scope)
    {
        var entries = scope.Elements.GetDictionary(category);
        if (entries == null)
            return null;

        var item = entries.Elements[name];
        if (item is PdfReference reference)
            item = reference.Value;

        return item is PdfNull || item is PdfNullObject ? null : item;
    }

    /// <summary>
    /// Notes that the stream has been read in this scope, and says whether it had not been read in
    /// this scope already.
    /// </summary>
    /// <remarks>
    /// The scope is part of what "already read" means. A stream with no resources of its own
    /// resolves its names against whatever drew it, so the same form reached once inside another
    /// form and once from the page is two different sets of names — and remembering only the stream
    /// would leave the second reading unmade. That matters to both callers:
    /// <see cref="PdfResourcePruner"/> would not record the names the form uses at page scope and
    /// could prune away an entry the page still draws with, and
    /// <see cref="PdfPageResourceUsage"/> would miss whatever is reachable only that way while
    /// still answering <see cref="Understood"/>. The depth bound, not this, is what stops a form
    /// drawing itself from running away.
    /// </remarks>
    private bool MarkAsRead(PdfDictionary stream, PdfDictionary scope)
    {
        // A direct stream cannot be shared and so cannot be drawn within itself.
        if (!stream.IsIndirect)
            return true;

        var key = new StreamInScope(stream.ObjectID, scope);
        if (_read.Contains(key))
            return false;

        _read.Add(key);
        return true;
    }

    /// <summary>
    /// A stream read in one particular scope. The scope is compared by identity rather than by
    /// value: two resource dictionaries holding the same entries are still two scopes, and
    /// comparing their contents would be both slow and wrong for a dictionary edited between
    /// readings.
    /// </summary>
    private readonly struct StreamInScope : IEquatable<StreamInScope>
    {
        private readonly PdfObjectID _stream;
        private readonly PdfDictionary _scope;

        internal StreamInScope(PdfObjectID stream, PdfDictionary scope)
        {
            _stream = stream;
            _scope = scope;
        }

        public bool Equals(StreamInScope other) =>
            _stream == other._stream && ReferenceEquals(_scope, other._scope);

        public override bool Equals(object obj) => obj is StreamInScope other && Equals(other);

        public override int GetHashCode() =>
            _stream.GetHashCode() ^ RuntimeHelpers.GetHashCode(_scope);
    }

    #endregion

    #region Content of a stream

    private static bool TryGetContent(PdfDictionary stream, out byte[] content)
    {
        return PdfContentStreams.TryGetContent(stream, out content);
    }

    #endregion
}
