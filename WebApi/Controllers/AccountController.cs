using Application.Common.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        [HttpGet("me")]
        public IActionResult Me() => Ok(new ResponseDto<object> { Data = new { fullName = User.Identity!.Name, swipeId = User.FindFirstValue("preferred_username") } });

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
