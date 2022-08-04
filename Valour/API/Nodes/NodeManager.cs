﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Valour.Api.Client;

namespace Valour.Api.Nodes
{
    public static class NodeManager
    {
        public static List<Node> Nodes { get; set; } = new List<Node>();
        public static Dictionary<long, Node> PlanetToNode { get; } = new Dictionary<long, Node>();
        public static Dictionary<string, Node> NameToNode { get; } = new Dictionary<string, Node>();

        const string CoreLocation = "https://core.valour.gg";

        public static void AddNode(Node node)
        {
            NameToNode[node.Name] = node;

            if (!Nodes.Any(x => x.Name == node.Name))
                Nodes.Add(node);
        }

        public static async ValueTask<Node> GetNodeForPlanetAsync(long planetId)
        {

#if (!DEBUG)
            PlanetToNode.TryGetValue(planetId, out Node node);

            // Do we already have the node?
            if (node is null)
            {
                //If not, ask core node where the planet is located
                HttpRequestMessage request = new()
                {
                    RequestUri = new Uri(CoreLocation + $"/nodes/planet/{planetId}/name"),
                    Method = HttpMethod.Get
                };

                WebAssemblyHttpRequestMessageExtensions.SetBrowserRequestMode(httpRequestMessage, BrowserRequestMode.NoCors);

                var coreResponse = await ValourClient.Http.GetAsync();

                // We failed to find the planet in a node
                if (!coreResponse.IsSuccessStatusCode)
                    return null;

                // If we succeeded, wrap the response in a node object
                var nodeName = await coreResponse.Content.ReadAsStringAsync();
                node = new Node();

                await node.InitializeAsync(nodeName, ValourClient.Token);

                // Put the node into the node list, and as a node for the planet
                Nodes.Add(node);
                PlanetToNode[planetId] = node;
                NameToNode[node.Name] = node;
            }

            return node;
#else
            // In debug, we only have one node (local)
            return Nodes[0];
#endif
        }
    }
}
