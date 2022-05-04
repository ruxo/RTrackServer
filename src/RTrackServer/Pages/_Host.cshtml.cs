using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RTrackServer.Domain;

namespace RTrackServer.Pages;

public class HostPage : PageModel
{
    public HostPage(IOptions<AppConfig> config) {
        Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    }
    
    public string Version { get; private set; }
}