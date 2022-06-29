﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Valour.Shared.Authorization;

namespace Valour.Shared.Items.Authorization;

/*  Valour - A free and secure chat client
*  Copyright (C) 2021 Vooper Media LLC
*  This program is subject to the GNU Affero General Public license
*  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
*/

public class AuthTokenBase
{
    /// <summary>
    /// The ID of the authentification key is also the secret key. Really no need for another random gen.
    /// </summary>
    [Key]
    public string Id { get; set; }

    /// <summary>
    /// The ID of the app that has been issued this token
    /// </summary>
    public string App_Id { get; set; }

    /// <summary>
    /// The user that this token is valid for
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The scope of the permissions this token is valid for
    /// </summary>
    public ulong Scope { get; set; }

    /// <summary>
    /// The time that this token was issued
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// The time that this token will expire
    /// </summary>
    public DateTime Expires { get; set; }

    /// <summary>
    /// Helper method for scope checking
    /// </summary>
    public bool HasScope(Permission permission) =>
        Permission.HasPermission(Scope, permission);
}

