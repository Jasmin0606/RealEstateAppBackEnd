using Microsoft.AspNetCore.Mvc;
using Shared.Constants;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    /// <summary>
    /// GET /api/countries — Returns all active countries.
    /// Frontend uses this to show country switcher and load correct fields/labels.
    /// </summary>
    [HttpGet]
    public IActionResult GetActive()
    {
        var active = SupportedCountries.All.Values
            .Select(c => new
            {
                c.CountryCode,
                c.CountryName,
                c.CurrencyCode,
                c.CurrencySymbol,
                c.DateFormat,
                c.PhonePrefix,
                c.LocationProvider,
                c.SizeUnit,
                c.PropertyTypes,
                c.Regions,
                c.ExtraFields
            })
            .ToList();

        return Ok(active);
    }

    /// <summary>
    /// GET /api/countries/{code} — Returns config for a single country.
    /// </summary>
    [HttpGet("{code}")]
    public IActionResult GetByCode(string code)
    {
        var config = SupportedCountries.GetByCode(code);
        return config is null ? NotFound() : Ok(config);
    }
}
