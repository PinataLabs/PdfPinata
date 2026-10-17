using System;
using System.Net.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SampleBlazorWasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Static files only: the fonts, the image and the encrypted document all come from wwwroot.
var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddSingleton(http);

// The library's seams are process-wide and the font resolver cannot be replaced once a font has
// been used, so they are set here, once, before any component runs - and nowhere else.
await Backends.RegisterAsync(http);

await builder.Build().RunAsync();
