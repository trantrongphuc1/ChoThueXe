using ChoThueXe.Data;
using ChoThueXe.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;

namespace ChoThueXe.Controllers;

public class AuthController : Controller
{
    private readonly IAuthRepository _authRepository;
    private readonly Services.EmailService _emailService;

    public AuthController(IAuthRepository authRepository, Services.EmailService emailService)
    {
        _authRepository = authRepository;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginInputModel());
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await _authRepository.AuthenticateAsync(input.Email, input.Password);
        if (user is null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View(input);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterInputModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        if (await _authRepository.EmailExistsAsync(input.Email))
        {
            ViewData["Error"] = "Email đã tồn tại.";
            return View(input);
        }

        try
        {
            await _authRepository.RegisterCustomerAsync(input);
            TempData["Success"] = "Đăng ký thành công, vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }
        catch (InvalidOperationException ex)
        {
            ViewData["Error"] = ex.Message;
            return View(input);
        }
        catch (OracleException)
        {
            ViewData["Error"] = "Đăng ký thất bại do lỗi hệ thống. Vui lòng thử lại.";
            return View(input);
        }
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ViewData["Error"] = "Email không được để trống.";
            return View();
        }

        try
        {
            var otpCode = await _authRepository.GenerateOtpAsync(email);
            await _emailService.SendOtpEmailAsync(email, otpCode);
            TempData["Success"] = $"OTP đã được gửi đến email {email}.";
            return RedirectToAction(nameof(ResetPassword));
        }
        catch (InvalidOperationException ex)
        {
            ViewData["Error"] = ex.Message;
            return View();
        }
        catch (OracleException)
        {
            ViewData["Error"] = "Không thể gửi OTP lúc này do lỗi hệ thống dữ liệu.";
            return View();
        }
        catch (Exception)
        {
            ViewData["Error"] = "Không thể gửi OTP lúc này. Vui lòng thử lại.";
            return View();
        }
    }

    [HttpGet]
    public IActionResult ResetPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string email, string otpCode, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(newPassword))
        {
            ViewData["Error"] = "Vui lòng nhập email, OTP và mật khẩu mới.";
            return View();
        }

        if (!await _authRepository.ValidateOtpAsync(email, otpCode))
        {
            ViewData["Error"] = "OTP không hợp lệ hoặc đã hết hạn.";
            return View();
        }

        try
        {
            await _authRepository.ResetPasswordAsync(email, newPassword);
            TempData["Success"] = "Mật khẩu đã được thay đổi. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(Login));
        }
        catch (InvalidOperationException ex)
        {
            ViewData["Error"] = ex.Message;
            return View();
        }
        catch (OracleException)
        {
            ViewData["Error"] = "Không thể đặt lại mật khẩu lúc này do lỗi hệ thống dữ liệu.";
            return View();
        }
        catch (Exception)
        {
            ViewData["Error"] = "Không thể đặt lại mật khẩu lúc này. Vui lòng thử lại.";
            return View();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
