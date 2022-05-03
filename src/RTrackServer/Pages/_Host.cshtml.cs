using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RTrackServer.Pages;

public class HostPage : PageModel
{
    public HostPage() {
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    }
    
    public string Version { get; private set; }
}