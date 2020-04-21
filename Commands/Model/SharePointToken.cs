﻿using System.Linq;

namespace SharePointPnP.PowerShell.Commands.Model
{
    /// <summary>
    /// Contains a SharePoint JWT oAuth token
    /// </summary>
    public class SharePointToken : GenericToken
    {
        /// <summary>
        /// Instantiates a new Graph token
        /// </summary> r
        /// <param name="accesstoken">Accesstoken of which to instantiate a new token</param>
        public SharePointToken(string accesstoken) : base(accesstoken)
        {
            TokenAudience = Enums.TokenAudience.SharePointOnline;
            Roles = ParsedToken.Claims.FirstOrDefault(c => c.Type == "scp").Value.Split(new string[] { " " }, System.StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
