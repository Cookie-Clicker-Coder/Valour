﻿using Valour.Api.Items.Authorization;
using Valour.Api.Items.Channels.Planets;

namespace Valour.Api.Requests;

public class CreatePlanetCategoryChannelRequest
{
    public PlanetCategoryChannel Category { get; set; }
    public List<PermissionsNode> Nodes { get; set; }
}

