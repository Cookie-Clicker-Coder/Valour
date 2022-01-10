﻿
using System.Text.Json.Serialization;

namespace Valour.Database.Items;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

public abstract class NamedItem : Item
{
    [JsonInclude]
    [JsonPropertyName("Name")]
    public string Name { get; set; }
}

