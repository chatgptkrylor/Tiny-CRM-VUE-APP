using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/interactions")]
public class InteractionsController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    public InteractionsController(TinyCrmDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<InteractionItem>> Create([FromBody] Interaction model)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == model.CustomerId);
        if (!customerExists) return NotFound();

        // Not a DataAnnotation: a business rule, checked here like the original controller did.
        if (model.InteractionDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(Interaction.InteractionDate), "Interaction date cannot be in the future.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var entity = new Interaction
        {
            CustomerId = model.CustomerId,
            Type = model.Type,
            Subject = model.Subject,
            Notes = model.Notes,
            InteractionDate = model.InteractionDate,
            CreatedAt = DateTime.Now,
        };
        _db.Interactions.Add(entity);
        await _db.SaveChangesAsync();

        await RecalculateLastInteractionDate(model.CustomerId);
        await _db.SaveChangesAsync();

        return Ok(new InteractionItem(
            entity.Id, entity.CustomerId, entity.Type, entity.Subject, entity.Notes,
            entity.InteractionDate, entity.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var interaction = await _db.Interactions.FindAsync(id);
        if (interaction is null) return NotFound();

        var customerId = interaction.CustomerId;
        _db.Interactions.Remove(interaction);
        await _db.SaveChangesAsync();

        await RecalculateLastInteractionDate(customerId);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task RecalculateLastInteractionDate(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return;

        customer.LastInteractionDate = await _db.Interactions
            .Where(i => i.CustomerId == customerId)
            .Select(i => (DateTime?)i.InteractionDate)
            .MaxAsync();
    }
}
