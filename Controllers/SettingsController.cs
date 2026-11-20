using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NEXT.Models;
using NEXT.Services;
using NEXT.ViewModels;

namespace NEXT.Controllers;

public class SettingsController : AppController
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IFileStorageService _files;

    public SettingsController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IFileStorageService files)
    {
        _users = users;
        _signIn = signIn;
        _files = files;
    }

    private static SettingsPageViewModel Build(ApplicationUser user) => new()
    {
        Theme = user.Theme,
        Profile = new EditProfileViewModel
        {
            FullName = user.FullName, UserName = user.UserName!, Email = user.Email ?? "", Bio = user.Bio,
            CurrentAvatar = user.ProfilePicturePath, CurrentCover = user.CoverPhotoPath
        }
    };

    [HttpGet]
    public async Task<IActionResult> Index() => View(Build((await _users.GetUserAsync(User))!));

    [HttpPost]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] EditProfileViewModel model)
    {
        var user = (await _users.GetUserAsync(User))!;

        IActionResult Invalid()
        {
            model.CurrentAvatar = user.ProfilePicturePath;
            model.CurrentCover = user.CoverPhotoPath;
            return View("Index", new SettingsPageViewModel { Theme = user.Theme, Profile = model });
        }

        // Only the profile form is posted here; the other forms' fields are not part of this request.
        if (!ModelState.IsValid) return Invalid();

        string? newAvatar = null, newCover = null;
        if (model.ProfilePicture is { Length: > 0 })
        {
            var saved = await _files.SaveImageAsync(model.ProfilePicture, "avatars");
            if (!saved.Succeeded) { ModelState.AddModelError("Profile.ProfilePicture", saved.Error!); return Invalid(); }
            newAvatar = saved.Data;
        }
        if (model.CoverPhoto is { Length: > 0 })
        {
            var saved = await _files.SaveImageAsync(model.CoverPhoto, "covers");
            if (!saved.Succeeded)
            {
                _files.Delete(newAvatar);
                ModelState.AddModelError("Profile.CoverPhoto", saved.Error!);
                return Invalid();
            }
            newCover = saved.Data;
        }

        var email = model.Email.Trim();
        var userName = model.UserName.Trim();

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var r = await _users.SetEmailAsync(user, email);
            if (!r.Succeeded) return FailWith(r, "Profile.Email");
        }
        if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
        {
            var r = await _users.SetUserNameAsync(user, userName);
            if (!r.Succeeded) return FailWith(r, "Profile.UserName");
        }

        user.FullName = model.FullName.Trim();
        user.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        if (newAvatar is not null) { _files.Delete(user.ProfilePicturePath); user.ProfilePicturePath = newAvatar; }
        if (newCover is not null) { _files.Delete(user.CoverPhotoPath); user.CoverPhotoPath = newCover; }

        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded) return FailWith(update, "Profile");

        await _signIn.RefreshSignInAsync(user); // username/security stamp may have changed
        Toast("Profile updated.");
        return RedirectToAction(nameof(Index));

        IActionResult FailWith(IdentityResult r, string key)
        {
            foreach (var e in r.Errors) ModelState.AddModelError(key, e.Description);
            _files.Delete(newAvatar);
            _files.Delete(newCover);
            return Invalid();
        }
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel model)
    {
        var user = (await _users.GetUserAsync(User))!;
        if (!ModelState.IsValid)
        {
            var page = Build(user);
            page.Password = model;
            return View("Index", page);
        }

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("Password.CurrentPassword", e.Description);
            var page = Build(user);
            page.Password = new ChangePasswordViewModel();
            return View("Index", page);
        }

        await _signIn.RefreshSignInAsync(user);
        Toast("Password changed.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetTheme(string theme)
    {
        var user = (await _users.GetUserAsync(User))!;
        user.Theme = theme == "light" ? "light" : "dark";
        await _users.UpdateAsync(user);
        Toast(user.Theme == "light" ? "Light mode on." : "Dark mode on.");
        return RedirectToAction(nameof(Index));
    }
}
