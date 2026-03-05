#nullable enable

namespace Bible.Alarm.Shared.Models.Enums;

/// <summary>
/// Defines the three types of cataloging logic for publications:
/// - Flat: Music and Video publications using GETPUBMEDIALINKS directly
/// - Sectioned: Bible and iam (Kingdom Melodies) with Section → Track structure
/// - MediatorSectioned: Dramas using Mediator API for discovery, then GETPUBMEDIALINKS with section codes
/// </summary>
public enum CatalogType
{
    /// <summary>
    /// Flat-track publications (Music/Video) using GETPUBMEDIALINKS directly
    /// </summary>
    Flat = 0,

    /// <summary>
    /// Sectioned publications with Section → Track structure.
    /// - Bible uses `booknum={sectionCode}`.
    /// - Melody disc-style section codes (e.g. `iam-9`) use `pub={sectionCode}` (no `booknum`).
    /// </summary>
    Sectioned = 1,

    /// <summary>
    /// Drama publications using Mediator API for discovery, then GETPUBMEDIALINKS with section codes
    /// </summary>
    MediatorSectioned = 2
}
