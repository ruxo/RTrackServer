using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RTrackServer.Pages;

public class Logout : PageModel
{
    public async Task<IActionResult> OnGet() {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }
}