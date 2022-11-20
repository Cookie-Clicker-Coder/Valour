﻿using System.Text.Json.Serialization;
using Valour.Api.Items.Messages.Embeds.Items;

namespace Valour.Api.Items.Messages.Embeds;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2022 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

public class PersonalEmbedUpdate
{
    [JsonPropertyName("TargetUserId")]
    public long TargetUserId { get; set; }

	[JsonPropertyName("TargetMessageId")]
	public long TargetMessageId { get; set; }

	[JsonPropertyName("NewEmbedContent")]
	public string NewEmbedContent { get; set; }
}