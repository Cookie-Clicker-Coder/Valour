using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valour.Shared.Planets
{
    /*  Valour - A free and secure chat client
     *  Copyright (C) 2020 Vooper Media LLC
     *  This program is subject to the GNU Affero General Public license
     *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
     */


    /// <summary>
    /// This represents a user within a planet and is used to represent membership
    /// </summary>
    public class ClientPlanetInvite
    {
        /// <summary>
        /// The Id of this object
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// the invite code
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// The planet the invite is for
        /// </summary>
        public ulong Planet_Id { get; set; }

        /// <summary>
        /// The user that created the invite
        /// </summary>
        public ulong User_Id { get; set; }

        /// <summary>
        /// The time the invite was created
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// The length of the invite before its invaild
        /// </summary>
        public int? hours { get; set; }

        /// <summary>
        /// True if the invite never expires
        /// </summary>
        public bool Permanent { get; set; }
    }
}