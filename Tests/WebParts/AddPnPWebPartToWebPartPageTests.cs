using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Management.Automation.Runspaces;

namespace SharePointPnP.PowerShell.Tests.WebParts
{

    [TestClass]
    public class AddWebPartToWebPartPageTests
    {
        #region Test Setup/CleanUp

        [TestInitialize]
        public void Initialize()
        {
            using (var scope = new PSTestScope())
            {
                // Example
                // scope.ExecuteCommand("cmdlet", new CommandParameter("param1", prop));
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var scope = new PSTestScope())
            {
                try
                {

                }
                catch (Exception)
                {
                    // Describe Exception
                }
            }
        }

        #endregion

        #region Scaffolded Cmdlet Tests

        

        //TODO: This is a scaffold of the cmdlet - complete the unit test
        //[TestMethod]
        public void AddPnPWebPartToWebPartPageTest()
        {
                                
            using (var scope = new PSTestScope(true))
            {
                // Complete writing cmd parameters
                var results = scope.ExecuteCommand("Add-PnPWebPartToWebPartPage",new CommandParameter("ServerRelativePageUrl", "null"),new CommandParameter("Xml", "null"),new CommandParameter("Path", "null"),new CommandParameter("ZoneId", "null"),new CommandParameter("ZoneIndex", "null"));
                Assert.IsNotNull(results);
            }

        }
            

        #endregion
    }
}
            