#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

public interface ICategoryService : IDisposable
{
    Task<List<Category>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
}
