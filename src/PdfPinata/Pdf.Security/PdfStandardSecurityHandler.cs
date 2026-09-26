#region Copyright
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@pdfsharp.com)
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne (Germany)
//
// http://www.pdfsharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Internal;


namespace PdfPinata.Pdf.Security;

/// <summary>
/// Represents the standard PDF security handler.
/// </summary>
public sealed class PdfStandardSecurityHandler : PdfSecurityHandler
{
    internal PdfStandardSecurityHandler(PdfDocument document)
        : base(document)
    { }

    internal PdfStandardSecurityHandler(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Sets the user password of the document. Setting a password automatically sets the
    /// PdfDocumentSecurityLevel to PdfDocumentSecurityLevel.Encrypted128Bit if its current
    /// value is PdfDocumentSecurityLevel.None.
    /// </summary>
    public string UserPassword
    {
        set
        {
            if (_document._securitySettings.DocumentSecurityLevel == PdfDocumentSecurityLevel.None)
                _document._securitySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            _userPassword = value;
        }
    }
    internal string _userPassword;

    /// <summary>
    /// Sets the owner password of the document. Setting a password automatically sets the
    /// PdfDocumentSecurityLevel to PdfDocumentSecurityLevel.Encrypted128Bit if its current
    /// value is PdfDocumentSecurityLevel.None.
    /// </summary>
    public string OwnerPassword
    {
        set
        {
            if (_document._securitySettings.DocumentSecurityLevel == PdfDocumentSecurityLevel.None)
                _document._securitySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            _ownerPassword = value;
        }
    }
    internal string _ownerPassword;

    /// <summary>
    /// Gets or sets the user access permission represented as an integer in the P key.
    /// </summary>
    internal PdfUserAccessPermission Permission
    {
        get
        {
            var permission = (PdfUserAccessPermission)Elements.GetInteger(Keys.P);
            if ((int)permission == 0)
                permission = PdfUserAccessPermission.PermitAll;
            return permission;
        }
        set => Elements.SetInteger(Keys.P, (int)value);
    }

    /// <summary>
    /// Encrypts the whole document.
    /// </summary>
    public void EncryptDocument()
    {
        foreach (var iref in _document._irefTable.AllReferences)
        {
            if (ReferenceEquals(iref.Value, this))
                continue;

            // An object that came out of an object stream has already had everything done to it
            // that needs doing. The stream holding it was decrypted as a unit, and PDF 32000-1
            // 7.5.7 says strings inside an object stream are not separately encrypted — so running
            // the decryption over them a second time is what turns a title into mojibake. This
            // affects any encrypted file that uses object streams, whoever wrote it; it only became
            // reachable here once this library could write one itself.
            if (iref.Value.IsFromObjectStream)
                continue;

            EncryptObject(iref.Value);
        }
    }

    /// <summary>
    /// Encrypts an indirect object.
    /// </summary>
    internal void EncryptObject(PdfObject value)
    {
        Debug.Assert(value.Reference != null);

        stringEncryptor.CreateHashKey(value.ObjectID);

        PdfDictionary dict;
        PdfArray array;
        PdfStringObject str;
        if ((dict = value as PdfDictionary) != null)
            EncryptDictionary(dict);
        else if ((array = value as PdfArray) != null)
            EncryptArray(array);
        else if ((str = value as PdfStringObject) != null)
        {
            if (str.Length != 0)
            {
                var bytes = str.EncryptionValue;
                bytes = stringEncryptor.Encrypt(bytes);
                str.EncryptionValue = bytes;
            }
        }
    }

    /// <summary>
    /// Encrypts a dictionary.
    /// </summary>
    private void EncryptDictionary(PdfDictionary dict)
    {
        // Pdf Reference 1.7, Chapter 7.5.8.2: The cross-reference stream shall not be encrypted
        // Pdf Reference 1.7, Chapter 7.6.1: Strings in the Encryption-Dictionary shall not be encrypted
        if (dict.Elements.GetName("/Type") == "/XRef"
            || dict.ObjectNumber == ObjectNumber)
            return;

        EncryptElements(dict);
        EncryptStream(dict);
    }

    /// <summary>
    /// Encrypts every string of a dictionary, and walks into the dictionaries and arrays it holds.
    /// </summary>
    private void EncryptElements(PdfDictionary dict)
    {
        // A PdfString is immutable, so a string this walk decrypts has to be put back where the old
        // one was rather than written into. The replacements are collected here and applied below,
        // after the enumeration has finished. Writing them as they are found would be fine on the
        // net8.0 and net10.0 legs — .NET Core stopped bumping a Dictionary's version on an
        // overwrite — but netstandard2.1 exists for Unity's Mono runtime, which still bumps it, and
        // there the same loop would throw halfway through decrypting a document.
        List<KeyValuePair<string, PdfString>> decrypted = null;

        foreach (var item in dict.Elements)
        {
            switch (item.Value)
            {
                case PdfString value1:
                    decrypted ??= [];
                    decrypted.Add(new KeyValuePair<string, PdfString>(item.Key, EncryptString(value1)));
                    break;

                case PdfDictionary value2:
                    EncryptDictionary(value2);
                    break;

                case PdfArray value3:
                    EncryptArray(value3);
                    break;
            }
        }

        if (decrypted == null)
            return;

        // Through the indexer, which is the one path that tells the owning object it changed.
        // The old setter reached past it and mutated the string the dictionary was still
        // holding, which is what made the replacement invisible to everything watching.
        foreach (var replacement in decrypted)
            dict.Elements[replacement.Key] = replacement.Value;
    }

    /// <summary>
    /// Encrypts the stream of a dictionary, if it has one with anything in it.
    /// </summary>
    private void EncryptStream(PdfDictionary dict)
    {
        if (dict.Stream == null)
            return;

        var bytes = dict.Stream.Value;
        if (bytes.Length == 0)
            return;

        streamEncryptor.CreateHashKey(dict.ObjectID);
        bytes = streamEncryptor.Encrypt(bytes);
        dict.Stream.Value = bytes;
    }

    /// <summary>
    /// Encrypts an array.
    /// </summary>
    private void EncryptArray(PdfArray array)
    {
        var count = array.Elements.Count;
        for (var idx = 0; idx < count; idx++)
        {
            var item = array.Elements[idx];
            PdfString value1;
            PdfDictionary value2;
            PdfArray value3;
            if ((value1 = item as PdfString) != null)
            {
                stringEncryptor.CreateHashKey(array.ObjectID);
                array.Elements[idx] = EncryptString(value1);
            }
            else if ((value2 = item as PdfDictionary) != null)
            {
                EncryptDictionary(value2);
            }
            else if ((value3 = item as PdfArray) != null)
            {
                EncryptArray(value3);
            }
        }
    }

    /// <summary>
    /// Encrypts a string, answering the string those bytes now spell.
    /// </summary>
    /// <remarks>
    /// A PdfString is a simple type and so immutable, which is why this answers a new string
    /// rather than writing back into the one it was given. The caller puts the answer where the
    /// old string was, through the owning collection's own indexer.
    /// </remarks>
    private PdfString EncryptString(PdfString value)
    {
        if (value.Length == 0)
            return value;

        var bytes = stringEncryptor.Encrypt(value.EncryptionValue);
        return PdfString.FromEncryptionValue(bytes, value.Flags);
    }

    /// <summary>
    /// Encrypts an array.
    /// </summary>
    internal byte[] EncryptBytes(byte[] bytes)
    {
        if (bytes != null && bytes.Length != 0)
        {
            _rc4.SetKey(_key, 0, _keySize);
            _rc4.Apply(bytes);
        }
        return bytes;
    }

    #region Encryption Algorithms

    /// <summary>
    /// Checks the password.
    /// </summary>
    /// <param name="inputPassword">Password or null if no password is provided.</param>
    public PasswordValidity ValidatePassword(string inputPassword)
    {
        // We can handle 40 and 128 bit standard encryption.
        var filter = Elements.GetName(PdfSecurityHandler.Keys.Filter);
        var v = Elements.GetInteger(PdfSecurityHandler.Keys.V);
        if (filter != "/Standard" || v is not (>= 1 and <= 5))
            throw new PdfReaderException(PSSR.UnknownEncryption);


        inputPassword ??= "";

        EncryptorFactory.Create(_document, this, out stringEncryptor, out streamEncryptor);
        stringEncryptor.InitEncryptionKey(inputPassword);
        if (!stringEncryptor.ValidatePassword(inputPassword))
            return PasswordValidity.Invalid;

        // Strings and streams can be covered by different crypt filters, but the document has
        // a single file encryption key, so the stream encryptor is given the key that was just
        // validated. Deriving it from the input password a second time would break an owner
        // password up to revision 4: the key belongs to the user password, which validation
        // recovers from the /O entry.
        streamEncryptor.EncryptionKey = stringEncryptor.EncryptionKey;

        return stringEncryptor.HaveOwnerPermission
            ? PasswordValidity.OwnerPassword
            : PasswordValidity.UserPassword;
    }

    /// <summary>
    /// Generates the user key based on the padded user password.
    /// </summary>
    private void InitWithUserPassword(byte[] documentID, string userPassword, byte[] ownerKey, int permissions, bool strongEncryption)
    {
        InitEncryptionKey(documentID, StandardSecurityAlgorithms.PadPassword(userPassword), ownerKey, permissions, strongEncryption);
        SetupUserKey(documentID);
    }

    /// <summary>
    /// Computes the padded user password from the padded owner password.
    /// </summary>
    private byte[] ComputeOwnerKey(byte[] userPad, byte[] ownerPad, bool strongEncryption)
    {
        var ownerKey = new byte[32];
        var digest = _md5.ComputeHash(ownerPad);
        if (strongEncryption)
        {
            var mkey = new byte[16];
            // Hash the pad 50 times
            for (var idx = 0; idx < 50; idx++)
                digest = _md5.ComputeHash(digest);
            Array.Copy(userPad, 0, ownerKey, 0, 32);
            // Encrypt the key
            for (var i = 0; i < 20; i++)
            {
                for (var j = 0; j < mkey.Length; ++j)
                    mkey[j] = (byte)(digest[j] ^ i);
                _rc4.SetKey(mkey);
                _rc4.Apply(ownerKey);
            }
        }
        else
        {
            _rc4.SetKey(digest, 0, 5);
            _rc4.Apply(userPad, 0, userPad.Length, ownerKey);
        }
        return ownerKey;
    }

    /// <summary>
    /// Computes the encryption key.
    /// </summary>
    private void InitEncryptionKey(byte[] documentID, byte[] userPad, byte[] ownerKey, int permissions, bool strongEncryption)
    {
        _ownerKey = ownerKey;
        _encryptionKey = new byte[strongEncryption ? 16 : 5];

        _md5.Initialize();
        _md5.TransformBlock(userPad, 0, userPad.Length, userPad, 0);
        _md5.TransformBlock(ownerKey, 0, ownerKey.Length, ownerKey, 0);

        // Split permission into 4 bytes
        var permission = new byte[4];
        permission[0] = (byte)permissions;
        permission[1] = (byte)(permissions >> 8);
        permission[2] = (byte)(permissions >> 16);
        permission[3] = (byte)(permissions >> 24);

        _md5.TransformBlock(permission, 0, 4, permission, 0);
        _md5.TransformBlock(documentID, 0, documentID.Length, documentID, 0);
        _md5.TransformFinalBlock(permission, 0, 0);
        var digest = _md5.Hash!;
        _md5.Initialize();
        // Create the hash 50 times (only for 128 bit)
        if (_encryptionKey.Length == 16)
        {
            for (var idx = 0; idx < 50; idx++)
            {
                digest = _md5.ComputeHash(digest);
                _md5.Initialize();
            }
        }
        Array.Copy(digest, 0, _encryptionKey, 0, _encryptionKey.Length);
    }

    /// <summary>
    /// Computes the user key.
    /// </summary>
    private void SetupUserKey(byte[] documentID)
    {
        if (_encryptionKey.Length == 16)
        {
            _md5.TransformBlock(StandardSecurityAlgorithms.PasswordPadding, 0, StandardSecurityAlgorithms.PasswordPadding.Length, StandardSecurityAlgorithms.PasswordPadding, 0);
            _md5.TransformFinalBlock(documentID, 0, documentID.Length);
            var digest = _md5.Hash!;
            _md5.Initialize();
            Array.Copy(digest, 0, _userKey, 0, 16);
            for (var idx = 16; idx < 32; idx++)
                _userKey[idx] = 0;
            //Encrypt the key
            for (var i = 0; i < 20; i++)
            {
                for (var j = 0; j < _encryptionKey.Length; j++)
                    digest[j] = (byte)(_encryptionKey[j] ^ i);
                _rc4.SetKey(digest, 0, _encryptionKey.Length);
                _rc4.Apply(_userKey, 0, 16, _userKey);
            }
        }
        else
        {
            _rc4.SetKey(_encryptionKey);
            _rc4.Apply(StandardSecurityAlgorithms.PasswordPadding, 0, StandardSecurityAlgorithms.PasswordPadding.Length, _userKey);
        }
    }

    /// <summary>
    /// Set the hash key for the specified object.
    /// </summary>
    internal void SetHashKey(PdfObjectID id)
    {
        _key = StandardSecurityAlgorithms.ObjectKey(_md5, _encryptionKey, id, aes: false, out _keySize);
    }

    /// <summary>
    /// Prepares the security handler for encrypting the document.
    /// </summary>
    public void PrepareEncryption()
    {
        Debug.Assert(_document._securitySettings.DocumentSecurityLevel != PdfDocumentSecurityLevel.None);
        var permissions = (int)Permission;
        var strongEncryption = _document._securitySettings.DocumentSecurityLevel == PdfDocumentSecurityLevel.Encrypted128Bit;

        PdfInteger vValue;
        PdfInteger length;
        PdfInteger rValue;

        if (strongEncryption)
        {
            vValue = new PdfInteger(2);
            length = new PdfInteger(128);
            rValue = new PdfInteger(3);
        }
        else
        {
            vValue = new PdfInteger(1);
            length = new PdfInteger(40);
            rValue = new PdfInteger(2);
        }

        if (string.IsNullOrEmpty(_userPassword))
            _userPassword = "";
        // Use user password twice if no owner password provided.
        if (string.IsNullOrEmpty(_ownerPassword))
            _ownerPassword = _userPassword;

        // Correct permission bits
        permissions |= (int)(strongEncryption ? 0xfffff0c0 : 0xffffffc0);
        permissions &= unchecked((int)0xfffffffc);

        var pValue = new PdfInteger(permissions);

        Debug.Assert(_ownerPassword.Length > 0, "Empty owner password.");
        var userPad = StandardSecurityAlgorithms.PadPassword(_userPassword);
        var ownerPad = StandardSecurityAlgorithms.PadPassword(_ownerPassword);

        _md5.Initialize();
        _ownerKey = ComputeOwnerKey(userPad, ownerPad, strongEncryption);
        var documentID = PdfEncoders.RawEncoding.GetBytes(_document.Internals.FirstDocumentID);
        InitWithUserPassword(documentID, _userPassword, _ownerKey, permissions, strongEncryption);

        // The owner and user entries carry key bytes, not text. They are named raw so that
        // the bytes above ASCII in them are written as they are instead of being taken for
        // characters and spelled out as UTF-16BE, which no reader could undo.
        var oValue = new PdfString(PdfEncoders.RawEncoding.GetString(_ownerKey, 0, _ownerKey.Length), PdfStringEncoding.RawEncoding);
        var uValue = new PdfString(PdfEncoders.RawEncoding.GetString(_userKey, 0, _userKey.Length), PdfStringEncoding.RawEncoding);

        Elements[PdfSecurityHandler.Keys.Filter] = new PdfName("/Standard");
        Elements[PdfSecurityHandler.Keys.V] = vValue;
        Elements[PdfSecurityHandler.Keys.Length] = length;
        Elements[Keys.R] = rValue;
        Elements[Keys.O] = oValue;
        Elements[Keys.U] = uValue;
        Elements[Keys.P] = pValue;
    }

    /// <summary>
    /// The global encryption key.
    /// </summary>
    private byte[] _encryptionKey;

    /// <summary>
    /// The MD5 implementation the standard security handler is built on. It is created on
    /// first use, because a handler is also instantiated for documents that are not encrypted.
    /// </summary>
    private MD5Managed _md5 => _md5Instance ??= new MD5Managed();
    private MD5Managed _md5Instance;

    /// <summary>
    /// The cipher the writer encrypts with.
    /// </summary>
    private readonly Rc4 _rc4 = new();

    /// <summary>
    /// The encryption key for the owner.
    /// </summary>
    private byte[] _ownerKey = new byte[32];

    /// <summary>
    /// The encryption key for the user.
    /// </summary>
    private readonly byte[] _userKey = new byte[32];

    /// <summary>
    /// The encryption key for a particular object/generation.
    /// </summary>
    private byte[] _key;

    /// <summary>
    /// The encryption key length for a particular object/generation.
    /// </summary>
    private int _keySize;

    private IEncryptor stringEncryptor;

    private IEncryptor streamEncryptor;

    #endregion

    internal override void WriteObject(PdfWriter writer)
    {
        // Don't encrypt myself.
        var securityHandler = writer.SecurityHandler;
        writer.SecurityHandler = null;
        base.WriteObject(writer);
        writer.SecurityHandler = securityHandler;
    }

    #region Keys
    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal sealed new class Keys : PdfSecurityHandler.Keys
    {
        /// <summary>
        /// (Required) A number specifying which revision of the standard security handler
        /// should be used to interpret this dictionary:
        /// • 2 if the document is encrypted with a V value less than 2 and does not have any of
        ///   the access permissions set (by means of the P entry, below) that are designated
        ///   "Revision 3 or greater".
        /// • 3 if the document is encrypted with a V value of 2 or 3, or has any "Revision 3 or
        ///   greater" access permissions set.
        /// • 4 if the document is encrypted with a V value of 4
        /// • 5 (ExtensionLevel 3) if the document is encrypted with a V value of 5
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string R = "/R";

        /// <summary>
        ///  (Required) A string used in computing the encryption key.
        ///  The value of the string depends on the value of the
        ///  revision number, the R entry described above.
        ///  • The value of R is 4 or less: A 32-byte string, based on both the owner and user passwords, that is used in
        ///    computing the encryption key and in determining whether a valid owner password was entered.
        ///  • The value for R is 5: (ExtensionLevel 3) A 48-byte string,  based on the owner and user passwords, that is used in
        ///    computing the encryption key and in determining whether a valid owner password was entered.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Required)]
        public const string O = "/O";

        /// <summary>
        /// (Required) A string based on the user password. The value
        /// of the string depends on the value of the revision number, the R entry described above.
        /// • The value of R is 4 or less: A 32-byte string, based on the user password, that is used in determining
        ///   whether to prompt the user for a password and, if so, whether a valid user or owner password was entered.
        /// • The value for R is 5: (ExtensionLevel 3) A 48-byte string, based on the user password, that is used in
        ///   determining whether to prompt the user for a password and, if so, whether a valid user password was entered.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Required)]
        public const string U = "/U";

        /// <summary>
        /// (Required) A set of flags specifying which operations are permitted when the document
        /// is opened with user access.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string P = "/P";

        /// <summary>
        /// (ExtensionLevel 3; required if R is 5)
        /// A 32-byte string, based on the owner and user passwords, that is used in computing the encryption key.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string OE = "/OE";

        /// <summary>
        /// (ExtensionLevel 3; required if R is 5)
        /// A 32-byte string, based on the user password, that is used in computing the encryption key.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string UE = "/UE";

        /// <summary>
        /// (ExtensionLevel 3; required if R is 5)
        /// A 16-byte string, encrypted with the file encryption key, that contains an encrypted copy of the permission flags.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Perms = "/Perms";

        /// <summary>
        /// (Optional; meaningful only when the value of V is 4 or 5; PDF 1.5) Indicates whether
        /// the document-level metadata stream is to be encrypted. Applications should respect this value.
        /// Default value: true.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string EncryptMetadata = "/EncryptMetadata";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;

    #endregion
}
