using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using POESKillTree.SkillTreeFiles;
using POESKillTree.ViewModels;
using POESKillTree.ViewModels.ItemAttribute;

namespace UnitTests
{
    [TestClass]
    public class TestCharacterSheet
    {
        public TestContext TestContext { get; set; }

        static SkillTree _tree;

        [ClassInitialize]
        public static void Initalize(TestContext testContext)
        {
            if (ItemDB.IsEmpty())
                ItemDB.Load(@"..\..\..\WPFSkillTree\Items.xml", true);
            _tree = SkillTree.CreateSkillTree(() => { Debug.WriteLine("Download started"); }, (dummy1, dummy2) => { }, () => { Debug.WriteLine("Download finished"); });
        }

        readonly Regex _backreplace = new Regex("#");
        string InsertNumbersInAttributes(KeyValuePair<string, List<float>> attrib)
        {
            return attrib.Value.Aggregate(attrib.Key, (current, f) => _backreplace.Replace(current, f.ToString(CultureInfo.InvariantCulture.NumberFormat), 1));
        }

        [DataSource("Microsoft.VisualStudio.TestTools.DataSource.XML", @"..\..\TestBuilds\Builds.xml", "TestBuild", DataAccessMethod.Sequential)]
        [TestMethod]
        public void TestBuild()
        {
            // Read build entry.
            string treeUrl = TestContext.DataRow["TreeURL"].ToString();
            int level = Convert.ToInt32(TestContext.DataRow["Level"]);
            string buildFile = @"..\..\TestBuilds\" + TestContext.DataRow["BuildFile"];
            List<string> expectDefense = new List<string>();
            List<string> expectOffense = new List<string>();
            if (TestContext.DataRow.Table.Columns.Contains("ExpectDefence"))
            {
                using (StringReader reader = new StringReader(TestContext.DataRow["ExpectDefence"].ToString()))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length > 0 && !line.StartsWith("#"))
                            expectDefense.Add(line.Trim());
                    }
                }
            }
            if (TestContext.DataRow.Table.Columns.Contains("ExpectOffence"))
            {
                using (StringReader reader = new StringReader(TestContext.DataRow["ExpectOffence"].ToString()))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length > 0 && !line.StartsWith("#"))
                            expectOffense.Add(line.Trim());
                    }
                        
                }
            }

            // Initialize structures.
            _tree.LoadFromURL(treeUrl);
            _tree.Level = level;

            string itemData = File.ReadAllText(buildFile);
            ItemAttributes itemAttributes = new ItemAttributes(itemData);
            Compute.Initialize(_tree, itemAttributes);

            // Compare defense properties.
            Dictionary<string, List<string>> defense = new Dictionary<string, List<string>>();
            if (expectDefense.Count > 0)
            {
                foreach (ListGroup grp in Compute.Defense())
                {
                    List<string> props = grp.Properties.Select(InsertNumbersInAttributes).ToList();
                    defense.Add(grp.Name, props);
                }

                List<string> group = null;
                foreach (string entry in expectDefense)
                {
                    if (entry.Contains(':')) // Property: Value
                    {
                        Assert.IsNotNull(group, "Missing defence group [" + TestContext.DataRow["BuildFile"] + "]");
                        Assert.IsTrue(group.Contains(entry), "Wrong " + entry + " [" + TestContext.DataRow["BuildFile"] + "]");
                    }
                    else // Group
                    {
                        Assert.IsTrue(defense.ContainsKey(entry), "No such defence group: " + entry + " [" + TestContext.DataRow["BuildFile"] + "]");
                        group = defense[entry];
                    }
                }
            }

            // Compare offense properties.
            Dictionary<string, List<string>> offense = new Dictionary<string, List<string>>();
            if (expectOffense.Count > 0)
            {
                foreach (ListGroup grp in Compute.Offense())
                {
                    List<string> props = grp.Properties.Select(InsertNumbersInAttributes).ToList();
                    offense.Add(grp.Name, props);
                }

                List<string> group = null;
                foreach (string entry in expectOffense)
                {
                    if (entry.Contains(':')) // Property: Value
                    {
                        Assert.IsNotNull(group, "Missing offence group [" + TestContext.DataRow["BuildFile"] + "]");
                        Assert.IsTrue(group.Contains(entry), "Wrong " + entry + " [" + TestContext.DataRow["BuildFile"] + "]");
                    }
                    else // Group
                    {
                        Assert.IsTrue(offense.ContainsKey(entry), "No such offence group: " + entry + " [" + TestContext.DataRow["BuildFile"] + "]");
                        group = offense[entry];
                    }
                }
            }
        }
    }
}
