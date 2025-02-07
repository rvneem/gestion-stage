﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Gestion_Stagiaire.Data;
using Gestion_Stagiaire.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Security.Claims;
using Gestion_Stagiaires.Models;
using Azure.Core;

namespace Gestion_Stagiaire.Controllers
{
    [Authorize]
    public class DemandeStagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private UserManager<IdentityUser> userManager;
        private readonly ILogger<DemandeStagesController> _logger;



        public DemandeStagesController(ApplicationDbContext context, ILogger<DemandeStagesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: DemandeStages
        public async Task<IActionResult> Index(string searchString)
        {
            // Récupérer l'ID de l'utilisateur connecté
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Récupérer le rôle de l'utilisateur
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Stocker le filtre actuel pour l'utiliser dans la vue
            ViewData["CurrentFilter"] = searchString;

            // Base de la requête avec les inclusions nécessaires
            var demandesStage = _context.DemandesStage
                .Include(d => d.Stagiaire)
                .Include(d => d.Type_Stage)
                .Include(d => d.Status)
                .Include(d => d.Departement)
                .AsQueryable();

            // Si l'utilisateur est un stagiaire, filtrer par son ID
            if (userRole == "Stagiaire")
            {
                demandesStage = demandesStage.Where(d => d.Stagiaire.Id.ToString() == userId);
            }

            // Appliquer le filtre de recherche si fourni
            if (!string.IsNullOrEmpty(searchString))
            {
                demandesStage = demandesStage.Where(d =>
                    d.Stagiaire.Nom.Contains(searchString)
                    || d.Stagiaire.Prenom.Contains(searchString)
                    || d.Type_Stage.Stage_Type.Contains(searchString)
                    || d.Status.Reponse.Contains(searchString)
                    || d.Departement.Nom_Departement.Contains(searchString));
            }

            // Retourner la liste filtrée à la vue
            return View(await demandesStage.ToListAsync());
        }


        // Export to Excel
        public async Task<IActionResult> ExportToExcel()
        {
            var demandeStages = await _context.DemandesStage
                .Include(d => d.Stagiaire)
                .Include(d => d.Type_Stage)
                .Include(d => d.Status)
                .Include(d => d.Departement)
                .ToListAsync();

            // Configure EPPlus to use the non-commercial license
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("DemandeStages");

            // Add headers
            worksheet.Cells[1, 1].Value = "Stagiaire";
            worksheet.Cells[1, 2].Value = "Type de Stage";
            worksheet.Cells[1, 3].Value = "Date Debut";
            worksheet.Cells[1, 4].Value = "Date Fin";
            worksheet.Cells[1, 5].Value = "Status";
            worksheet.Cells[1, 6].Value = "Demande Stage";
            worksheet.Cells[1, 7].Value = "Date Demande";
            worksheet.Cells[1, 8].Value = "Departement";
            worksheet.Cells[1, 9].Value = "Encadrant";
            worksheet.Cells[1, 10].Value = "Titre Projet";
            worksheet.Cells[1, 11].Value = "Rapport PFE";
            worksheet.Cells[1, 12].Value = "Commentaire";

            // Add values
            for (int i = 0; i < demandeStages.Count; i++)
            {
                var row = i + 2;
                worksheet.Cells[row, 1].Value = demandeStages[i].Stagiaire.Nom + " " + demandeStages[i].Stagiaire.Prenom;
                worksheet.Cells[row, 2].Value = demandeStages[i].Type_Stage?.Stage_Type;
                worksheet.Cells[row, 3].Value = demandeStages[i].Date_Debut.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 4].Value = demandeStages[i].Date_Fin.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 5].Value = demandeStages[i].Status?.Reponse;
                worksheet.Cells[row, 6].Value = demandeStages[i].Path_Demande_Stage;
                worksheet.Cells[row, 7].Value = demandeStages[i].Date_Demande.ToString("yyyy-MM-dd");
                worksheet.Cells[row, 8].Value = demandeStages[i].Departement?.Nom_Departement;
                worksheet.Cells[row, 9].Value = demandeStages[i].Encadrant;
                worksheet.Cells[row, 10].Value = demandeStages[i].Titre_Projet;
                worksheet.Cells[row, 11].Value = demandeStages[i].Path_Rapport;
                worksheet.Cells[row, 12].Value = demandeStages[i].Commentaire;
            }

            var stream = new MemoryStream(package.GetAsByteArray());

            var content = stream.ToArray();
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fileName = "DemandeStages.xlsx";

            return File(content, contentType, fileName);
        }

        // GET: DemandeStages/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var demandeStage = await _context.DemandesStage
                .Include(d => d.Stagiaire)
                .Include(d => d.Type_Stage)
                .Include(d => d.Status)
                .Include(d => d.Departement)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (demandeStage == null)
            {
                return NotFound();
            }

            return View(demandeStage);
        }

        [HttpGet("current-user")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var userInfo = new
            {
                Id = user.Id,
            };

            return Ok(userInfo);
        }

        // GET: DemandeStages/Create
        public async Task<IActionResult> Create()
        {
            var demandeStage = new DemandeStage
            {
                Date_Demande = DateTime.Now,
                Date_Debut = DateTime.MinValue,
                Date_Fin = DateTime.MinValue
            };

            // Check if the user is authenticated
            if (User.Identity.IsAuthenticated)
            {
                var claims = User.Claims.ToList();
                foreach (var claim in claims)
                {
                    Console.WriteLine($"{claim.Type}: {claim.Value}");
                }
            }
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var existingStagiaire = await _context.Stagiaires
    .FirstOrDefaultAsync(s => s.Id == Guid.Parse(userId));

            if (existingStagiaire == null && User.IsInRole("Stagiaire"))
            {
                // The StagiaireId doesn't exist, handle accordingly (e.g., add new entry or throw an error)
                ModelState.AddModelError(string.Empty, "The specified Stagiaire ID does not exist.");
            }

            //  var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var stagiaire = await _context.Stagiaires
                                           .FirstOrDefaultAsync(s => s.Id == Guid.Parse(userId)); // Assuming 'UserId' is a foreign key

            if (stagiaire != null)
            {
                ViewBag.StagiaireId = stagiaire.Id;
                ViewBag.StagiaireNomPrenom = $"{stagiaire.Nom} {stagiaire.Prenom}"; // Add Prenom to display full name
            }
            else
            {
                // You can log or return a message to help debug why the user wasn't found
                Console.WriteLine("Stagiaire not found for user ID: " + userId);
            }


            if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var stagiaireId))
            {
                ViewData["StagiaireId"] = new SelectList(
                    _context.Stagiaires
                        .Where(s => s.Id == stagiaireId)
                        .Select(s => new
                        {
                            Id = s.Id,
                            FullName = s.Nom + " " + s.Prenom
                        }),
                    "Id",
                    "FullName"
                );
            }
            ViewData["Stagiaire"] = new SelectList(_context.Stagiaires.Select(s => new
            {
                Id = s.Id,
                FullName = s.Nom + " " + s.Prenom
            }), "Id", "FullName");

            // Return the view with the current data
            ViewData["Type_StageId"] = new SelectList(_context.TypesStage, "Id", "Stage_Type");
            ViewData["StatusId"] = new SelectList(_context.Statuses, "Id", "Reponse");
            ViewData["DepartementId"] = new SelectList(_context.Departements, "Id", "Nom_Departement");

            return View(demandeStage);
        }

        // POST: DemandeStages/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DemandeStage demandeStage, IFormFile? Path_Demande_Stage, IFormFile? Path_Rapport)
        {
            /*  
              var currentUser = await _userManager.GetUserAsync(User);

              if (currentUser == null)
              {
                  return RedirectToAction("AccessDenied", "Account");
              }

              // Fetch the StagiaireId for the logged-in user
              var stagiaire = await _context.Stagiaires
                  .Where(s => s.Id.ToString() == currentUser.Id)
                  .Select(s => new { s.Id, s.Nom, s.Prenom })
                  .FirstOrDefaultAsync();

              if (stagiaire == null)
              {
                  return RedirectToAction("AccessDenied", "Account");
              }
              var userRoles = await _userManager.GetRolesAsync(currentUser);
              var isStagiaire = userRoles.Contains("Stagiaire");
              var isAdmin = userRoles.Contains("Admin");

              // Pass Stagiaire information to the view
              ViewBag.StagiaireId = stagiaire.Id;
              ViewBag.StagiaireNomPrenom = $"{stagiaire.Nom} {stagiaire.Prenom}";
            */

            if (ModelState.IsValid)
            {    // Get the current user

                //currentUser = User.Identity.Name;

                // Handle Demande Stage File upload
                if (Path_Demande_Stage != null && Path_Demande_Stage.Length > 0)
                {
                    var fileExtension = Path.GetExtension(Path_Demande_Stage.FileName).ToLower();
                    if (fileExtension != ".pdf")
                    {
                        ModelState.AddModelError("Path_Demande_Stage", "Le fichier de demande de stage doit être un fichier .pdf.");
                        PopulateDropDownLists(demandeStage);
                        return View(demandeStage);
                    }

                    var uploadsFolder = Path.Combine("wwwroot/uploads/demandes");
                    Directory.CreateDirectory(uploadsFolder);

                    var filePath = Path.Combine(uploadsFolder, $"{demandeStage.StagiaireId}.pdf");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await Path_Demande_Stage.CopyToAsync(stream);
                    }

                    demandeStage.Path_Demande_Stage = $"{demandeStage.StagiaireId}.pdf";
                }

                // Handle Rapport PFE File upload
                if (Path_Rapport != null && Path_Rapport.Length > 0)
                {
                    var fileExtension = Path.GetExtension(Path_Rapport.FileName).ToLower();
                    if (fileExtension != ".pdf")
                    {
                        ModelState.AddModelError("Path_Rapport", "Le fichier du rapport PFE doit être un fichier .pdf.");
                        PopulateDropDownLists(demandeStage);
                        return View(demandeStage);
                    }

                    var uploadsFolder = Path.Combine("wwwroot/uploads/rapports");
                    Directory.CreateDirectory(uploadsFolder);

                    var filePath = Path.Combine(uploadsFolder, $"{demandeStage.StagiaireId}_rapport.pdf");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await Path_Rapport.CopyToAsync(stream);
                    }

                    demandeStage.Path_Rapport = $"{demandeStage.StagiaireId}_rapport.pdf";
                }

                // Si le statut est null, lui attribuer "En cours" par défaut
                if (demandeStage.StatusId == null)
                {
                    var statutEnCours = await _context.Statuses.FirstOrDefaultAsync(s => s.Reponse == "En cours");
                    if (statutEnCours != null)
                    {
                        demandeStage.StatusId = statutEnCours.Id;
                    }
                    else
                    {
                        ModelState.AddModelError("", "Le statut 'En cours' n'existe pas dans la base de données. Veuillez le créer.");
                        PopulateDropDownLists(demandeStage);
                        return View(demandeStage);
                    }
                }

                _context.Add(demandeStage);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            PopulateDropDownLists(demandeStage);
            return View(demandeStage);
        }


        // GET: DemandeStages/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Récupérer la demande de stage depuis la base de données
            var demandeStage = await _context.DemandesStage
                    .Include(d => d.Stagiaire) // Include Stagiaire for the navigation property
        .FirstOrDefaultAsync(d => d.Id == id);
            if (demandeStage == null)
            {
                return NotFound();
            }

            // Assurez-vous de passer les valeurs nécessaires à la vue
            PopulateDropDownLists(demandeStage);

            // Renvoyer la vue avec les données de la demande de stage
            return View(demandeStage);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,StagiaireId,Type_StageId,Date_Debut,Date_Fin,StatusId,Date_Demande,Encadrant,DepartementId,Commentaire,Titre_Projet,Path_Demande_Stage,Path_Rapport")] DemandeStage demandeStage, IFormFile? Path_Demande_Stage, IFormFile? Path_Rapport)
        {
            if (id != demandeStage.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Retrieve the existing record from the database
                    var existingDemandeStage = await _context.DemandesStage.FindAsync(id);
                    if (existingDemandeStage == null)
                    {
                        return NotFound();
                    }

                    // Validate Type_StageId
                    var typeStageExists = await _context.TypesStage.AnyAsync(ts => ts.Id == demandeStage.Type_StageId);
                    if (!typeStageExists)
                    {
                        ModelState.AddModelError("Type_StageId", "Le type de stage sélectionné n'existe pas.");
                        PopulateDropDownLists(demandeStage);
                        return View(demandeStage);
                    }

                    // Handle Demande Stage File upload
                    if (Path_Demande_Stage != null && Path_Demande_Stage.Length > 0)
                    {
                        var fileExtension = Path.GetExtension(Path_Demande_Stage.FileName).ToLower();
                        if (fileExtension != ".pdf")
                        {
                            ModelState.AddModelError("Path_Demande_Stage", "Le fichier de demande de stage doit être un fichier .pdf.");
                            PopulateDropDownLists(demandeStage);
                            return View(demandeStage);
                        }

                        var uploadsFolder = Path.Combine("wwwroot/uploads/demandes");
                        Directory.CreateDirectory(uploadsFolder);

                        var filePath = Path.Combine(uploadsFolder, $"{demandeStage.StagiaireId}.pdf");
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await Path_Demande_Stage.CopyToAsync(stream);
                        }

                        demandeStage.Path_Demande_Stage = $"{demandeStage.StagiaireId}.pdf";
                    }
                    else
                    {
                        // Preserve existing file path
                        demandeStage.Path_Demande_Stage = existingDemandeStage.Path_Demande_Stage;
                    }

                    // Handle Rapport PFE File upload
                    if (Path_Rapport != null && Path_Rapport.Length > 0)
                    {
                        var fileExtension = Path.GetExtension(Path_Rapport.FileName).ToLower();
                        if (fileExtension != ".pdf")
                        {
                            ModelState.AddModelError("Path_Rapport", "Le fichier du rapport PFE doit être un fichier .pdf.");
                            PopulateDropDownLists(demandeStage);
                            return View(demandeStage);
                        }

                        var uploadsFolder = Path.Combine("wwwroot/uploads/rapports");
                        Directory.CreateDirectory(uploadsFolder);

                        var filePath = Path.Combine(uploadsFolder, $"{demandeStage.StagiaireId}_rapport.pdf");
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await Path_Rapport.CopyToAsync(stream);
                        }

                        demandeStage.Path_Rapport = $"{demandeStage.StagiaireId}_rapport.pdf";
                    }
                    else
                    {
                        // Preserve existing file path
                        demandeStage.Path_Rapport = existingDemandeStage.Path_Rapport;
                    }

                    // Update other properties manually
                    _context.Entry(existingDemandeStage).CurrentValues.SetValues(demandeStage);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DemandeStageExists(demandeStage.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState)
                {
                    _logger.LogWarning($"Key: {error.Key}, Errors: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                }
                PopulateDropDownLists(demandeStage);
                return View(demandeStage);
            }

            PopulateDropDownLists(demandeStage);
            return View(demandeStage);
        }

        // Helper Method
        private bool DemandeStageExists(Guid id)
        {
            return _context.DemandesStage.Any(e => e.Id == id);
        }

        private void PopulateDropDownLists(DemandeStage demandeStage = null)
        {
            ViewData["StagiaireId"] = new SelectList(_context.Stagiaires.Select(s => new { s.Id, FullName = s.Nom + " " + s.Prenom }), "Id", "FullName", demandeStage?.StagiaireId);
            ViewData["Type_StageId"] = new SelectList(_context.TypesStage, "Id", "Stage_Type", demandeStage?.Type_StageId);
            ViewData["StatusId"] = new SelectList(_context.Statuses, "Id", "Reponse", demandeStage?.StatusId);
            ViewData["DepartementId"] = new SelectList(_context.Departements, "Id", "Nom_Departement", demandeStage?.DepartementId);
        }
        // GET: DemandeStages/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var demandeStage = await _context.DemandesStage
                .Include(d => d.Stagiaire)
                .Include(d => d.Type_Stage)
                .Include(d => d.Status)
                .Include(d => d.Departement)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (demandeStage == null)
            {
                return NotFound();
            }

            return View(demandeStage);
        }

        // POST: DemandeStages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var demandeStage = await _context.DemandesStage.FindAsync(id);
            if (demandeStage != null)
            {
                // Delete associated files if they exist
                if (!string.IsNullOrEmpty(demandeStage.Path_Demande_Stage))
                {
                    var demandeFilePath = Path.Combine("wwwroot/uploads/demandes", demandeStage.Path_Demande_Stage);
                    if (System.IO.File.Exists(demandeFilePath))
                    {
                        System.IO.File.Delete(demandeFilePath);
                    }
                }

                if (!string.IsNullOrEmpty(demandeStage.Path_Rapport))
                {
                    var rapportFilePath = Path.Combine("wwwroot/uploads/rapports", demandeStage.Path_Rapport);
                    if (System.IO.File.Exists(rapportFilePath))
                    {
                        System.IO.File.Delete(rapportFilePath);
                    }
                }

                _context.DemandesStage.Remove(demandeStage);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }


    }
}