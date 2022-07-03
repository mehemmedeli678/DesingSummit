using DesingSummit.Data;
using DesingSummit.Models;
using DesingSummit.VM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesingSummit.Areas.Jury.Controllers
{
    [Area("Jury")]
    [Authorize(Roles ="Admin,Jury")]
    public class HomeController : Controller
    {
        private readonly DesignDbContext _context;
        private readonly UserManager<SummitUser> _userManager;
        public HomeController(DesignDbContext context, UserManager<SummitUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var lang = HttpContext.Features.Get<IRequestCultureFeature>().RequestCulture.Culture.ToString().ToLower();
            var info = await _context.Infos.FirstOrDefaultAsync(x => x.LangCode == lang);
            var user = await _userManager.GetUserAsync(User);
            var designs = await _context.Designs.Include(x=>x.DesignPictures).Include(x=>x.Category).ThenInclude(x=>x.CategoryLanguages.Where(x=>x.LangCode==lang))
                .Where(x=>x.IsActive)
                .Skip(0).Take(6).Select(x=>new DesignVM()
                {
                     AddDate = x.AddDate,
                     CategoryID = x.CategoryID,
                     Category=x.Category,
                     Description = x.Description,
                     DesignPictures=x.DesignPictures,
                     Header=x.Header,
                     ID=x.ID,
                     IsActive=x.IsActive,
                     IsWinner=x.IsWinner,
                     JuryVotes=x.JuryVotes,
                     Picture=x.DesignPictures.FirstOrDefault(x=>x.PictureUrl != null).PictureUrl,
                     Point=x.Point,
                     Score=x.JuryVotes.FirstOrDefault(x=>x.SummitUserId==user.Id),
                     SummitUserId=x.SummitUserId
                }).ToListAsync();
            JuryVM vm = new()
            {
                DesignVM = designs,
                Info = info
            };
            return View(vm);
        }


        [HttpPost("/jury/load")]
        public async Task<IActionResult> Load(int count)
        {
            var lang = HttpContext.Features.Get<IRequestCultureFeature>().RequestCulture.Culture.ToString().ToLower();
            var user = await _userManager.GetUserAsync(User);
            JsonResult res = new(new() { });
            var designs = await _context.Designs.Include(x => x.DesignPictures).Include(x => x.Category).ThenInclude(x => x.CategoryLanguages.Where(x=>x.LangCode==lang))
                .Where(x => x.IsActive)
                .Skip(count).Take(6).Select(x => new DesignVM()
                {
                    AddDate = x.AddDate,
                    CategoryID = x.CategoryID,
                    Category = x.Category,
                    Description = x.Description,
                    DesignPictures = x.DesignPictures,
                    Header = x.Header,
                    ID = x.ID,
                    IsActive = x.IsActive,
                    IsWinner = x.IsWinner,
                    JuryVotes = x.JuryVotes,
                    Picture = x.DesignPictures.FirstOrDefault(x => x.PictureUrl != null).PictureUrl,
                    Point = x.JuryVotes.Sum(x=>x.Score),
                    Score = x.JuryVotes.FirstOrDefault(x => x.SummitUserId == user.Id),
                    SummitUserId = x.SummitUserId
                }).ToListAsync();
            res.Value = designs;
            return Json(res);
        }

        [HttpPost("/jury/vote")]
        public async Task<IActionResult> Vote(int vote,int id)
        {
            JsonResult res = new(new() { });
            if (vote < 0 || vote > 25)
            {
                res.Value = null;
                return Json(res);
            };
            var user = await _userManager.GetUserAsync(User);

            var samevote = _context.JuryVotes.FirstOrDefault(x => x.DesignID == id && x.SummitUserId == user.Id && x.Score==vote);
            if (samevote!=null)
            {
                samevote.Score = 0;
                _context.JuryVotes.Update(samevote);
                _context.SaveChanges();
                res.Value = 0;
                return Json(res);
            }
            var invaliddesign = _context.JuryVotes.FirstOrDefault(x => x.DesignID == id && x.SummitUserId == user.Id);
            if (invaliddesign != null)
            {
                invaliddesign.Score = vote;
                _context.JuryVotes.Update(invaliddesign);
                _context.SaveChanges();
                res.Value = vote;
                return Json(res);
            }
            JuryVote juryVote = new()
            {
                DesignID = id,
                SummitUserId = user.Id,
                Score = vote,
            };
            _context.JuryVotes.Add(juryVote);
            _context.SaveChanges();
            res.Value =vote;
            return Json(res);
        }
        [HttpGet("/jury/detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var lang = HttpContext.Features.Get<IRequestCultureFeature>().RequestCulture.Culture.ToString().ToLower();
            var info = await _context.Infos.FirstOrDefaultAsync(x=>x.LangCode==lang);
            var design = _context.Designs.Include(x=>x.SummitUser).Include(x=>x.DesignPictures).Include(x=>x.Category).ThenInclude(x=>x.CategoryLanguages).FirstOrDefault(x => x.ID == id);
            if (design == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            IList<string> roles= await _userManager.GetRolesAsync(user);
            ViewData["Admin"] =roles;
            var vote = await _context.JuryVotes.FirstOrDefaultAsync(x => x.DesignID == design.ID && x.SummitUserId == user.Id);
            ViewData["Vote"] = vote?.Score;

            DetailVM vm = new DetailVM()
            {
                Info = info,
                Design = design
            };
            return View(vm);
        }
    }
}