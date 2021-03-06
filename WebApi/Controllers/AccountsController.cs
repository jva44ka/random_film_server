using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Models;
using WebApi.ViewModels.RequestModels;
using WebApi.ViewModels.ResultModels;
using Infrastructure.Exceptions;
using Services.Managers.Interfaces;
using AutoMapper;
using WebApi.ViewModels;
using System.Linq;
using Infrastructure.Auth;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountManager<Account> _accountManager;
        private readonly IMapper _mapper;

        public AccountsController(IAccountManager<Account> accountManager, IMapper mapper)
        {
            _accountManager = accountManager;
            _mapper = mapper;
        }

        // GET: api/Accounts
        [HttpGet]
        [Authorize]
        public async Task<IList<AccountViewModel>> GetAccounts()
        {
            var accounts = await _accountManager.GetAll();
            return _mapper.Map<IList<Account>, IList<AccountViewModel>>(accounts);
        }

        // GET: api/Accounts/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<AccountViewModel>> GetAccount(string id)
        {
            var account = await _accountManager.GetUserById(id);

            if (account == null)
                return NotFound();

            return _mapper.Map<Account, AccountViewModel>(account);
        }

        // GET: api/Accounts/self
        [HttpGet("self")]
        [Authorize]
        public async Task<ActionResult<AccountViewModel>> GetSelfAccount()
        {
            var userId = HttpContext.User.Claims.FirstOrDefault(c => c.Type == AuthExtensions.UserId).Value;
            var account = await _accountManager.GetUserById(userId);

            if (account == null)
                return NotFound();

            return _mapper.Map<Account, AccountViewModel>(account);
        }

        // POST: api/Accounts/login
        [HttpPost("login")]
        public async Task<LoginResult> Login([FromBody]LoginRequest request)
        {
            if (string.IsNullOrEmpty(request?.UsernameOrEmail) || string.IsNullOrEmpty(request?.Password))
                throw new MissingParametersException("request is null or contains empty param");

            var signInResult = await _accountManager.SignInAsync(request.UsernameOrEmail, request.Password);
            var result = new LoginResult();
            if (signInResult?.User != null && signInResult.SignInResult.Succeeded)
            {
                result.LoggedIn = true;
                result.AccessToken = _accountManager.GenerateToken(signInResult.User);
                result.UserId = signInResult.User.Id;
            }
            else if (signInResult?.SignInResult.IsLockedOut == true)
                throw new LoginException("User is banned");

            /*else
            {
                LoginException ex = new LoginException("Wrong username/password");
                if (signInResult?.User != null)
                    ex.AccessFailedCount = signInResult.User.AccessFailedCount;

                throw ex;
            }*/
            return result;
        }

        // POST: api/Accounts/Create
        [HttpPost]
        public async Task<ActionResult<string>> CreateAccount([FromBody]CreateAccountRequest request)
        {
            var newAccount = _mapper.Map<CreateAccountRequest, Account>(request);
            var registerResult = await _accountManager.CreateUserAsync(newAccount, request.Password, request.SignInAfter);
            var result = new JsonResult(registerResult);
            return result;
        }

        // PUT: api/Accounts/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<AccountViewModel>> PutAccount(string id, AccountViewModel accountVM)
        {
            var requestingUserId = HttpContext.User.Claims.FirstOrDefault(c => c.Type == AuthExtensions.UserId).Value;
            // Добавить еще и случай если админ
            if (id != requestingUserId)
                throw new PermissionException("Недостаточно прав");

            var account = _mapper.Map<AccountViewModel, Account>(accountVM);
            var updatedUser = await _accountManager.UpdateAsync(id, account);
            var result = _mapper.Map<Account, AccountViewModel>(updatedUser);
            return result;
        }

        // PUT: api/Accounts/self
        [HttpPut("self")]
        [Authorize]
        public async Task<ActionResult<AccountViewModel>> PutSelfAccount(AccountViewModel accountVM)
        {
            var requestingUserId = HttpContext.User.Claims.FirstOrDefault(c => c.Type == AuthExtensions.UserId).Value;
            var account = _mapper.Map<AccountViewModel, Account>(accountVM);
            var updatedUser = await _accountManager.UpdateAsync(requestingUserId, account);
            var result = _mapper.Map<Account, AccountViewModel>(updatedUser);
            return result;
        }

        // DELETE: api/Accounts/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> DeleteAccount(string id)
        {
            await _accountManager.DeleteAsync(id);
            return Ok();
        }
    }
}