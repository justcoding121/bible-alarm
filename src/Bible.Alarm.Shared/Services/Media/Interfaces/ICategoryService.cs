#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing Category database operations.
/// </summary>
public interface ICategoryService : IDisposable
{
    /// <summary>
    /// Gets all categories.
    /// </summary>
    Task<List<Category>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
}
