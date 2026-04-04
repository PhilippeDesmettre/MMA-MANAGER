using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;

namespace MmaManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaysController(MmaContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var pays = await db.Pays
            .OrderBy(p => p.Nom)
            .Select(p => new { p.PaysID, p.Nom, p.Code, p.Continent })
            .ToListAsync();

        return Ok(pays);
    }
}
