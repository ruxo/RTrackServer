using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace RTrackServer.Pages;

public class HostPage : PageModel
{
    public HostPage(IConfiguration config) {
        Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        BaseUri = (config["BasePath"] ?? string.Empty) + "/";
    }
    
    public string Version { get; private set; }
    public string BaseUri { get; private set; }
}