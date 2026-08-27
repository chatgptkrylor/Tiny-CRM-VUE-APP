using System.Web.Mvc;
using TinyCrm.Data.Repositories;
using TinyCrm.Infrastructure;
using TinyCrm.Models;

namespace TinyCrm.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserRepository _users = new UserRepository();

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValidField("Username") || !ModelState.IsValidField("Password"))
            {
                return View(model);
            }

            var user = _users.FindUser(model.Username);
            if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            Session["UserId"] = user.Id;
            Session["Username"] = user.Username;
            Session["DisplayName"] = user.DisplayName;

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        public class LoginModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}