﻿using System.IO;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Joobie.Data;
using Joobie.Models.JobModels;
using Joobie.Models;
using Joobie.Utility;
using LinqKit;
using System.Security.Claims;
using Joobie.ViewModels;
using Joobie.Infrastructure;
using Microsoft.AspNetCore.Http;


namespace Joobie.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string _cVFilePath = "/Joobie/Joobie/wwwroot/cVs";
        private readonly SearchStringSession _searchStringSession;
        private static byte _pageSize = 5;

        public HomeController(ApplicationDbContext context, SearchStringSession searchStringSession)
        {
            _context = context;
            _searchStringSession = searchStringSession;
        }

        // GET: Jobs
        public async Task<IActionResult> Index(SearchSettingViewModel searchSettingViewModel, int page = 1)
        {

            if (searchSettingViewModel.WorkingHour.Length == 0 && searchSettingViewModel.TypesOfContracts.Length == 0 &&
                searchSettingViewModel.Categories.Length == 0 && searchSettingViewModel.SearchString == "" && searchSettingViewModel.CitySearchString == "")
            {
                if (_searchStringSession.searchSetting == null)
                {
                    searchSettingViewModel = await SetSearchSettingViewModel();
                    _searchStringSession.SetSearch(searchSettingViewModel);
                }
            }
            else
            {
                _searchStringSession.SetSearch(searchSettingViewModel);
            }

            searchSettingViewModel = _searchStringSession.searchSetting;

            var jobs = await GetSortedAndFilteredJobListAsync(searchSettingViewModel);
            int totalJobs = jobs.Count();

            jobs = jobs.OrderBy(c => c.AddedDate)
                       .Skip((page - 1) * _pageSize)
                       .Take(_pageSize)
                       .ToList();

            ViewData["searchSettingViewModel"] = searchSettingViewModel;

            var viewModel = new ListViewModel<Job>
            {
                //searchStringSession = _searchStringSession,
                Items = jobs,
                PagingInfo = new PagingInfo
                {
                    CurrentPage = (byte)page,
                    ItemsPerPage = _pageSize,
                    TotalItems = (byte)totalJobs
                }
            };

            return View(viewModel);


            if (User.IsInRole(Strings.AdminUser))
            {
                return View(viewModel);
            }

            return View("ReadOnlyList", viewModel);
        }

        private async Task<SearchSettingViewModel> SetSearchSettingViewModel()
        {
            var categories = await _context.Category.ToListAsync();
            var workingHours = await _context.WorkingHours.ToListAsync();
            var typesOfContracts = await _context.TypeOfContract.ToListAsync();

            var searchSettingViewModel = new SearchSettingViewModel
            {
                Categories = new Filter[categories.Count],
                TypesOfContracts = new Filter[typesOfContracts.Count],
                WorkingHour = new Filter[workingHours.Count]
            };

            for (int i = 0; i < categories.Count; i++)
            {
                searchSettingViewModel.Categories[i] =
                    new Filter { Id = categories[i].Id, Name = categories[i].Name, Selected = false };
            }
            for (int i = 0; i < workingHours.Count; i++)
            {
                searchSettingViewModel.WorkingHour[i] =
                    new Filter { Id = workingHours[i].Id, Name = workingHours[i].Name, Selected = false };
            }
            for (int i = 0; i < typesOfContracts.Count; i++)
            {
                searchSettingViewModel.TypesOfContracts[i] =
                    new Filter { Id = typesOfContracts[i].Id, Name = typesOfContracts[i].Name, Selected = false };
            }

            return searchSettingViewModel;
        }

        private async Task<IEnumerable<Job>> GetSortedAndFilteredJobListAsync(SearchSettingViewModel searchSettingViewModel)
        {
            var predicate = PredicateBuilder.New<Job>();
            predicate.DefaultExpression = j => true;
            if (!string.IsNullOrEmpty(searchSettingViewModel.SearchString))
            {
                predicate = predicate.And(j => j.Name.Contains(searchSettingViewModel.SearchString));
            }
            if (!string.IsNullOrEmpty(searchSettingViewModel.CitySearchString))
            {
                predicate = predicate.And(j => j.Localization.Contains(searchSettingViewModel.CitySearchString));
            }
            if (searchSettingViewModel.Categories.Any(c => c.Selected == true))
            {
                List<int> catIds = new List<int>();
                for (int i = 0; i < searchSettingViewModel.Categories.Length; i++)
                {
                    if (searchSettingViewModel.Categories[i].Selected == true)
                        catIds.Add(searchSettingViewModel.Categories[i].Id);
                }
                if (catIds.Any())
                    predicate = predicate.And(j => catIds.Contains(j.CategoryId));
            }
            if (searchSettingViewModel.TypesOfContracts.Any(c => c.Selected == true))
            {
                List<int> typesIds = new List<int>();
                for (int i = 0; i < searchSettingViewModel.TypesOfContracts.Length; i++)
                {
                    if (searchSettingViewModel.TypesOfContracts[i].Selected == true)
                        typesIds.Add(searchSettingViewModel.TypesOfContracts[i].Id);
                }
                if (typesIds.Any())
                    predicate = predicate.And(j => typesIds.Contains(j.TypeOfContractId));
            }
            if (searchSettingViewModel.WorkingHour.Any(c => c.Selected == true))
            {
                List<int> workingHoursIds = new List<int>();
                for (int i = 0; i < searchSettingViewModel.WorkingHour.Length; i++)
                {
                    if (searchSettingViewModel.WorkingHour[i].Selected == true)
                        workingHoursIds.Add(searchSettingViewModel.WorkingHour[i].Id);
                }
                if (workingHoursIds.Any())
                    predicate = predicate.And(j => workingHoursIds.Contains(j.WorkingHoursId));
            }
            predicate = predicate.And(j => j.ApplicationUser.Name != null);

            var jobs = await _context.Job.Where(predicate)
                                 .Include(j => j.Category)
                                 .Include(j => j.TypeOfContract)
                                 .Include(j => j.WorkingHours)
                                 .Include(j => j.ApplicationUser)
                                 .ToListAsync();
            return jobs;
        }



        public async Task<IActionResult> Apply(long Id)
        {
            var job = await _context.Job.Include(j => j.Category)
                .Include(j => j.TypeOfContract)
                .Include(j => j.WorkingHours)
                .Include(j => j.ApplicationUser)
                .Where(j => j.Id == Id)
                .FirstOrDefaultAsync();
            CVJobApplicationUser cVJobApplicationUser = new CVJobApplicationUser
            {
                Job = job,
                JobsId=Id
            };
            return View(cVJobApplicationUser);
        }


        // POST: CVJobApplicationUsers/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        /*[HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(byte Id)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            string userId = claim.Value;
            CVJobApplicationUser add = new CVJobApplicationUser
            {
                ApplicationUserId = userId,
                JobsId = Id
            };


            if (ModelState.IsValid)
            {
                _context.Add(add);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("Index");
        }*/

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(CVJobApplicationUser cVJobApplicationUser)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            string userId = claim.Value;
            cVJobApplicationUser.ApplicationUserId = userId;
            var uniqueName = "";
            bool saveImageSuccess = true;
            if (!ModelState.IsValid)
            {
                return View("Apply", cVJobApplicationUser);
            }
            var cVJobApplicationUserInDb = await _context.CVJobApplicationUser.FirstOrDefaultAsync(c => c.ApplicationUserId == userId && c.JobsId== cVJobApplicationUser.JobsId);
            if (cVJobApplicationUserInDb != null)
            {   uniqueName = cVJobApplicationUserInDb.CvName;
                cVJobApplicationUserInDb.Job = cVJobApplicationUser.Job;
                cVJobApplicationUserInDb.ApplicationUser = cVJobApplicationUser.ApplicationUser;

                if (Request.Form.Files.Any())
                    saveImageSuccess = await SaveCvToDirectory(uniqueName);
                if (saveImageSuccess == false)
                    return View("Error");

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            

            uniqueName = await GetUniqueFileName();
            cVJobApplicationUser.CvName = Path.GetFileNameWithoutExtension(uniqueName)
                + Path.GetExtension(cVJobApplicationUser.Cv.FileName);
            saveImageSuccess = await SaveCvToDirectory(cVJobApplicationUser.CvName);
            if (saveImageSuccess == false)
                return View("Error");
            await _context.CVJobApplicationUser.AddAsync(cVJobApplicationUser);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> SaveCvToDirectory(string fileName)
        {
            IFormFile file = Request.Form.Files.First();
            string pathSrc = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()));
            pathSrc += _cVFilePath;
            using (var stream = new FileStream(Path.Combine(pathSrc, fileName), FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return true;
        }


        private async Task<string> GetUniqueFileName()
        {
            string fileName = "";
            await Task.Run(() =>
            {
                fileName = Path.GetRandomFileName();
                string path = Path.Combine("~/Data/Cvs", fileName);
                while (System.IO.File.Exists(path))
                {
                    fileName = Path.GetRandomFileName();
                    path = Path.Combine("~/Data/Cvs", fileName);
                }
            });

            return fileName;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> Details(long Id)
        {
            var job = await _context.Job.Include(j => j.Category)
                .Include(j => j.TypeOfContract)
                .Include(j => j.WorkingHours)
                .Include(j => j.ApplicationUser)
                .Where(j => j.Id == Id)
                .FirstOrDefaultAsync();
            CVJobApplicationUser cVJobApplicationUser = new CVJobApplicationUser
            {
                Job = job,
                JobsId = Id
            };
            return View(cVJobApplicationUser);
        }
    }
}
