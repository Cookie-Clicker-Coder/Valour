﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

namespace Valour.Shared.Users
{
    /// <summary>
    /// This is the base User object, which contains everything needed for public use
    /// </summary>
    public class User
    {
        /// <summary>
        /// The Id of the user
        /// </summary>
        [Key]
        [JsonPropertyName("Id")]
        public ulong Id { get; set; }

        /// <summary>
        /// The main display name for the user
        /// </summary>
        [JsonPropertyName("Username")]
        public string Username { get; set; }

        /// <summary>
        /// The url for the user's profile picture
        /// </summary>
        [JsonPropertyName("Pfp_Url")]
        public string Pfp_Url { get; set; }

        /// <summary>
        /// The Date and Time that the user joined Valour
        /// </summary>
        [JsonPropertyName("Join_DateTime")]
        public DateTime Join_DateTime { get; set; }

        /// <summary>
        /// True if the user is a bot
        /// </summary>
        [JsonPropertyName("Bot")]
        public bool Bot { get; set; }

        /// <summary>
        /// True if the account has been disabled
        /// </summary>
        [JsonPropertyName("Disabled")]
        public bool Disabled { get; set; }

        /// <summary>
        /// True if this user is a member of the Valour official staff team. Falsely modifying this 
        /// through a client modification to present non-official staff as staff is a breach of our
        /// license. Don't do that.
        /// </summary>
        [JsonPropertyName("Valour_Staff")]
        public bool Valour_Staff { get; set; }

        /// <summary>
        /// The integer representation of the current user state
        /// </summary>
        [JsonPropertyName("UserState_Value")]
        public int UserState_Value { get; set; }

        /// <summary>
        /// The last time this user was flagged as active (successful auth)
        /// </summary>
        [JsonPropertyName("Last_Active")]
        public DateTime Last_Active { get; set; }

        /// <summary>
        /// The span of time from which the user was last active
        /// </summary>
        [JsonPropertyName("Last_Active_Span")]
        public TimeSpan Last_Active_Span 
        { 
            get
            {
                return DateTime.UtcNow.Subtract(Last_Active);
            } 
        }

        [NotMapped]
        public UserState UserState
        {
            get
            {
                // Automatically determine
                if (UserState_Value == 0)
                {
                    double minPassed = DateTime.UtcNow.Subtract(Last_Active).TotalMinutes;

                    if (minPassed < 3)
                    {
                        return UserState.Online;
                    }
                    else if (minPassed < 6)
                    {
                        return UserState.Away;
                    }
                    else
                    {
                        return UserState.Offline;
                    }
                }

                // User selected
                return UserState.States[UserState_Value];
            }
            set
            {
                UserState_Value = value.Value;
            }
        }

        public static User Victor = new User()
        {
            Bot = true,
            UserState_Value = 4,
            Pfp_Url = "/media/victor-cyan.png",
            Username = "Victor",
            Valour_Staff = true,
            Id = ulong.MaxValue
        };

    }

}
