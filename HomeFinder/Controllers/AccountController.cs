using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    private readonly HomeFinderContext _context;

    public AccountController(HomeFinderContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(LoginViewModel model)
    {
        if (string.IsNullOrEmpty(model.UserType))
        {
            ViewBag.Error = "Выберите тип пользователя";
            return View();
        }

        if (model.UserType == "admin")
        {
            var admin = _context.Administrators
                .FirstOrDefault(a => a.Login == model.Login && a.Password == model.Password);

            if (admin != null)
            {
                HttpContext.Session.SetInt32("AdminId", admin.AdministratorId);
                HttpContext.Session.SetString("UserRole", "Admin");
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                ViewBag.Error = "Неверный логин или пароль администратора";
                return View();
            }
        }
        else if (model.UserType == "landlord")
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Login == model.Login &&
                                    u.Password == model.Password &&
                                    u.IsLandlord == true);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("UserRole", "Landlord");
                HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");

                var sub = _context.LandlordSubscriptions
                    .FirstOrDefault(x => x.UserId == user.UserId);

                if (sub != null && sub.Status == "active")
                    HttpContext.Session.SetString("IsPremium", "1");
                else
                    HttpContext.Session.SetString("IsPremium", "0");

                return RedirectToAction("MyApartments", "Apartments");
            }

            ViewBag.Error = "Неверный логин или пароль владельца";
            return View();
        }
        else if (model.UserType == "tenant")
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Login == model.Login &&
                                    u.Password == model.Password &&
                                    u.IsTenant == true);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("UserRole", "Tenant");
                HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.Error = "Неверный логин или пароль арендатора";
                return View();
            }
        }

        ViewBag.Error = "Неверный тип пользователя";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
