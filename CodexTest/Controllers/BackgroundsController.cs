using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmaManager.Data;
using MmaManager.Models;

namespace MmaManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackgroundsController(MmaContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Background>> GetAll()
        => await db.Backgrounds.OrderBy(b => b.Nom).ToListAsync();
}
