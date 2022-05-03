using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RTrackServer.Pages;

public class LoginModel : PageModel
{
    public Task OnGet(string redirectUri) => HttpContext.ChallengeAsync("oidc", new(){ RedirectUri = redirectUri });
}