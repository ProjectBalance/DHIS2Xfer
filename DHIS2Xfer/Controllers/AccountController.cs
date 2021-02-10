using System.Collections.Generic;
using System.Security.Claims;
using DHIS2Xfer.Factory;
using DHIS2Xfer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace DHIS2Xfer.Controllers
{
    public class AccountController : Controller
    {
        public string userKey;
        public IConfiguration Configuration { get; }

        public AccountController(IConfiguration config)
        {
            Configuration = config;
            userKey = Configuration["FileDirectory"];
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login([Bind] User user)
        {
            string staticSalt = "hdT6deKj65TTu+e44EBHDCyDd34RR+33ExafdEFFDhv=";
            string login = user.UserName + "|" + user.Password;
            string hashedPW = SecurityFactory.hashString(login,staticSalt);
            if (hashedPW == Configuration["User"])
            {
                var userClaims = new List<Claim>()
                {
                    new Claim(ClaimTypes.Name,user.UserName),
                };

                var identity = new ClaimsIdentity(userClaims, "User Identity");

                var userPrincipal = new ClaimsPrincipal(new[] { identity });
                HttpContext.SignInAsync(userPrincipal);

                return RedirectToAction("Dashboard", "Xfer");
            }

            ViewBag.Message = "UserName and Password combination is incorrect";
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}