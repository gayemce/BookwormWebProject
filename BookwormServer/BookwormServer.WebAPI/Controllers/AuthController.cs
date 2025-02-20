﻿using Azure.Core;
using BookwormServer.WebAPI.Context;
using BookwormServer.WebAPI.Dtos;
using BookwormServer.WebAPI.Models;
using BookwormServer.WebAPI.Services;
using BookwormServer.WebAPI.Validators;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookwormServer.WebAPI.Controllers;
[Route("api/[controller]/[action]")]
[ApiController]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly JwtService _jwtService;

    public AuthController(AppDbContext context, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, JwtService jwtService)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginDto request, CancellationToken cancellationToken)
    {
        LoginValidator validator = new();
        ValidationResult validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            return StatusCode(422, validationResult.Errors.Select(s => s.ErrorMessage));
        }

        AppUser? appUserByUsername = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == request.UserNameOrEmail);
        AppUser? appUserByEmail = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.UserNameOrEmail);

        AppUser? appUser = appUserByUsername ?? appUserByEmail;

        if (appUser is null)
        {
            return BadRequest(new { Message = "Kullanıcı bulunamadı!" });
        }

        var passwordHasher = new PasswordHasher<AppUser>();
        var passwordVerificationResult = passwordHasher.VerifyHashedPassword(appUser, appUser.PasswordHash!, request.Password);

        if(passwordVerificationResult == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { Message = "Şifreniz yanlış!" });
        }

        string token = _jwtService.CreateToken(appUser, null ,request.RemeberMe);
        return Ok(new { AccessToken = token });
    }

    [HttpGet("{userId}")]
    public IActionResult GetUser(int userId)
    {
        var user = _context.AppUsers.Where(p => p.Id == userId).FirstOrDefault();
        return Ok(user);
    }

    [HttpPost]
    public IActionResult UpdateUserInformation(UpdateUserInformationDto request)
    {
        var user = _context.AppUsers.Find(request.Id);
        if (user is null)
        {
            return BadRequest(new { Message = "Kayıt Bulunamadı!" });
        }

        UpdateUserInformationValidator informationValidator = new();
        ValidationResult validationResult = informationValidator.Validate(request);

        if (!validationResult.IsValid)
        {
            return StatusCode(422, validationResult.Errors.Select(s => s.ErrorMessage));
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.UserName = request.UserName;
        user.Email = request.Email;

        _context.SaveChanges();
        return NoContent();
    }

    [HttpPost]
    public IActionResult UpdateUserPassword(UpdateUserPasswordDto request)
    {
        var user = _context.AppUsers.Find(request.Id);
        if (user is null)
        {
            return BadRequest(new { Message = "Kayıt Bulunamadı!" });
        }

        UpdateUserPasswordValidator passwordValidator = new();
        ValidationResult validationResult = passwordValidator.Validate(request);

        if (!validationResult.IsValid)
        {
            return StatusCode(422, validationResult.Errors.Select(s => s.ErrorMessage));
        }

        var passwordHasher = new PasswordHasher<AppUser>();
        var passwordVerificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (passwordVerificationResult != PasswordVerificationResult.Success)
        {
            return BadRequest(new { Message = "Mevcut şifre yanlış!" });
        }

        if (request.NewPassword != request.ConfirmedPassword)
        {
            return BadRequest(new { Message = "Yeni şifreler uyuşmuyor!" });
        }

        var hashedNewPassword = passwordHasher.HashPassword(null, request.NewPassword);
        user.PasswordHash = hashedNewPassword;

        _context.SaveChanges();
        return NoContent();
    }


    [HttpPost]
    public async Task<IActionResult> Register(RegisterDto request, CancellationToken cancellationToken)
    {
        RegisterValidator validator = new();
        ValidationResult validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            return StatusCode(422, validationResult.Errors.Select(s => s.ErrorMessage));
        }

        bool userNameExist = await _userManager.Users.AnyAsync(p => p.UserName == request.UserName);
        bool emailExist = await _userManager.Users.AnyAsync(p => p.Email == request.Email);

        if (!userNameExist && !emailExist)
        {
            if(request.Password != request.ConfirmedPassword)
            {
                return BadRequest(new { Message = "Başarısız kayıt işlemi. Şifreler uyuşmuyor!" });
            }

            var passwordHasher = new PasswordHasher<AppUser>();
            var hashedPassword = passwordHasher.HashPassword(null!, request.Password);

            AppUser appUser = new()
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                EmailConfirmed = true,
                UserName = request.UserName,
                PasswordHash = hashedPassword,
            };

            _context.Add(appUser);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kayıt işlemi başarıyla tamamlandı." });
        }

        return BadRequest(new { Message = "Bu kullanıcı kayıdı zaten mevcut"});

    }
}

