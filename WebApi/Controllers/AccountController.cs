using Application.Common.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        [HttpGet("me")]
        public IActionResult Me() => Ok(new ResponseDto<object> { Data = new { fullName = User.Identity!.Name } });

        // A full page load from the Vue app: it hands the browser to the DRMCAppManagement login page.
        [AllowAnonymous]
        [HttpGet("login")]
        public IActionResult Login(string? returnUrl) => Challenge(
            new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/" },
            OpenIdConnectDefaults.AuthenticationScheme);

        [AllowAnonymous]
        [HttpGet("portal")]
        public IActionResult Portal([FromServices] IConfiguration configuration) => Redirect(configuration["Sso:Authority"]!);
    }
}
