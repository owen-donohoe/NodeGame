using System.Reflection;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// Settings persistence, which is mostly a question about defaults.
    ///
    /// The interesting case throughout is that a struct of zeroes is a real
    /// thing a player can choose - every volume down, every switch off - and
    /// is also exactly what JsonUtility produces for a save written before
    /// settings existed. GameSettingsData.version is the only thing that tells
    /// those apart, so most of what follows is about that boundary.
    /// </summary>
    public class GameSettingsTests
    {
        // ===== MIGRATION =====

        [Test]
        public void CreateDefault_EnablesOpponentEmotesAndIsCurrentVersion()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefault();
            Assert.AreEqual(GameSettingsData.CurrentVersion, defaults.version);
            Assert.IsTrue(defaults.opponentEmotes);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Normalized_OlderSettings_EnableEmotesAndKeepPreferences(int version)
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.version = version;
            stored.opponentEmotes = false;
            stored.opponentRoutes = false;
            stored.musicVolume = 0.12f;
            stored.reducedMotion = true;

            GameSettingsData result = GameSettingsData.Normalized(stored);
            Assert.AreEqual(GameSettingsData.CurrentVersion, result.version);
            Assert.IsTrue(result.opponentEmotes);
            Assert.AreEqual(version < 2, result.opponentRoutes);
            Assert.AreEqual(0.12f, result.musicVolume);
            Assert.IsTrue(result.reducedMotion);
        }

        [Test]
        public void Normalized_CurrentVersion_PreservesEmotesOff()
        {
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.opponentEmotes = false;
            Assert.IsFalse(GameSettingsData.Normalized(stored).opponentEmotes);
        }

        // ===== FRAME CAP =====

        [Test]
        public void CreateDefault_CapsAtSixty()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefault();

            Assert.AreEqual(GameSettingsData.DefaultFrameCap, defaults.frameCap);
            Assert.AreEqual(60, GameSettingsData.TargetFrameRate(defaults.frameCap),
                "the cap the build had before it was a setting");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Normalized_OlderSave_GetsTheFrameCapDefaultNotItsZero(int version)
        {
            // A save from before the field has 0 there, which as an index
            // would silently become 30 fps. Every older version must get 60.
            GameSettingsData stored = GameSettingsData.CreateDefault();
            stored.version = version;
            stored.frameCap = 0;
            stored.musicVolume = 0.12f;
            stored.reducedMotion = true;

            GameSettingsData result = GameSettingsData.Normalized(stored);

            Assert.AreEqual(GameSettingsData.DefaultFrameCap, result.frameCap);
            Assert.AreEqual(0.12f, result.musicVolume, "the migration must not reset what the player set");
            Assert.IsTrue(result.reducedMotion);
        }

        [Test]
        public void Normalized_VersionZero_GetsTheFrameCapDefault()
        {
            Assert.AreEqual(GameSettingsData.DefaultFrameCap,
                GameSettingsData.Normalized(new GameSettingsData()).frameCap);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Normalized_CurrentVersion_KeepsEveryValidFrameCap(int stored)
        {
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.frameCap = stored;

            Assert.AreEqual(stored, GameSettingsData.Normalized(settings).frameCap);
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(99)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Normalized_OutOfRangeFrameCap_FallsBackToTheDefault(int stored)
        {
            // The default, not the nearest end: a corrupt value must not
            // become "uncapped" or "30".
            GameSettingsData settings = GameSettingsData.CreateDefault();
            settings.frameCap = stored;

            Assert.AreEqual(GameSettingsData.DefaultFrameCap, GameSettingsData.Normalized(settings).frameCap);
        }

        [TestCase(0, 30)]
        [TestCase(1, 60)]
        [TestCase(2, 120)]
        [TestCase(3, -1)]
        public void TargetFrameRate_MapsEachIndex(int index, int rate)
        {
            Assert.AreEqual(rate, GameSettingsData.TargetFrameRate(index));
        }

        [TestCase(-5)]
        [TestCase(4)]
        public void TargetFrameRate_OutOfRange_IsTheDefaultRate(int index)
        {
            Assert.AreEqual(60, GameSettingsData.TargetFrameRate(index));
        }

        [Test]
        public void FrameCapRates_AreAscendingAndUncappedIsLast()
        {
            // Cycling reads as "slower to faster to off"; -1 in the middle
            // would make the row jump around.
            Assert.AreEqual(GameSettingsData.FrameCapCount, GameSettingsData.FrameCapRates.Length);
            int last = GameSettingsData.FrameCapCount - 1;
            Assert.AreEqual(-1, GameSettingsData.FrameCapRates[last]);
            for (int i = 1; i < last; i++)
                Assert.Greater(GameSettingsData.FrameCapRates[i], GameSettingsData.FrameCapRates[i - 1]);
        }

        [Test]
        public void FrameCapLabel_NamesEveryIndexAndTheFallback()
        {
            for (int i = 0; i < GameSettingsData.FrameCapCount; i++)
                Assert.IsNotEmpty(GameSettingsData.FrameCapLabel(i));

            Assert.AreEqual("Uncapped", GameSettingsData.FrameCapLabel(3));
            Assert.AreEqual("60", GameSettingsData.FrameCapLabel(-1));
        }

        [Test]
        public void NextFrameCap_CyclesEveryEntryAndReturns()
        {
            int index = 0;
            for (int i = 0; i < GameSettingsData.FrameCapCount; i++)
                index = GameSettingsData.NextFrameCap(index);

            Assert.AreEqual(0, index);
            Assert.AreEqual(1, GameSettingsData.NextFrameCap(0));
            Assert.AreEqual(0, GameSettingsData.NextFrameCap(GameSettingsData.FrameCapCount - 1));
        }

        [TestCase(-3)]
        [TestCase(77)]
        public void NextFrameCap_ClampsBeforeStepping(int stored)
        {
            // A corrupt index becomes the default, then steps from it.
            Assert.AreEqual(GameSettingsData.DefaultFrameCap + 1, GameSettingsData.NextFrameCap(stored));
        }

        [Test]
        public void Differ_DetectsAFrameCapChange()
        {
            GameSettingsData a = GameSettingsData.CreateDefault();
            GameSettingsData b = a;
            b.frameCap = GameSettingsData.NextFrameCap(a.frameCap);

            Assert.IsTrue(GameSettingsData.Differ(a, b));
        }

        [Test]
        public void Normalized_Version3Save_KeepsEverythingElseAndTakesTheFrameCapDefault()
        {
            // The shape a real v3 save has on disk: every field but the new one.
            GameSettingsData stored = new GameSettingsData
            {
                version = 3,
                masterVolume = 0.2f,
                interfaceSize = 2,
                opponentRoutes = false,
                opponentEmotes = false,
                batterySaver = true
            };

            GameSettingsData result = GameSettingsData.Normalized(stored);

            Assert.AreEqual(GameSettingsData.CurrentVersion, result.version);
            Assert.AreEqual(GameSettingsData.DefaultFrameCap, result.frameCap);
            Assert.AreEqual(0.2f, result.masterVolume);
            Assert.AreEqual(2, result.interfaceSize);
            Assert.IsFalse(result.opponentRoutes, "off is a choice at version 3");
            Assert.IsFalse(result.opponentEmotes);
            Assert.IsTrue(result.batterySaver);
        }

        [Test]
        public void Normalized_VersionZero_BecomesDefaults()
        {
            // What a save written before settings existed deserialises to.
            GameSettingsData absent = new GameSettingsData();
            Assert.AreEqual(0, absent.version, "precondition: an unset struct has no version");

            GameSettingsData result = GameSettingsData.Normalized(absent);

            Assert.AreEqual(GameSettingsData.CreateDefault(), result);
        }

        [Test]
        public void Normalized_AllZeroAtCurrentVersion_KeepsTheZeroes()
        {
            // The distinction the version field exists for: this player turned
            // everything off on purpose, and must not be handed the defaults.
            GameSettingsData silent = new GameSettingsData
            {
                version = GameSettingsData.CurrentVersion
            };

            GameSettingsData result = GameSettingsData.Normalized(silent);

            Assert.AreEqual(0f, result.masterVolume);
            Assert.AreEqual(0f, result.musicVolume);
            Assert.AreEqual(0f, result.effectsVolume);
            Assert.IsFalse(result.colourblindMarks);
            Assert.IsFalse(result.haptics);
            Assert.AreEqual(0, result.frameCap, "index 0 is 30 fps, a real choice at the current version");
            Assert.AreEqual(GameSettingsData.CurrentVersion, result.version);
        }

        [Test]
        public void Normalized_Version1_KeepsWhatThePlayerSet()
        {
            // The migration that matters: a version 1 save is not a stranger
            // to be replaced, it is a player's settings missing one field.
            GameSettingsData stored = new GameSettingsData
            {
                version = 1,
                masterVolume = 0.1f,
                musicVolume = 0.2f,
                effectsVolume = 0.3f,
                colourblindMarks = false,
                reducedMotion = true,
                interfaceSize = 2,
                cameraSpeed = 0.9f,
                confirmEachCommand = true,
                haptics = false,
                batterySaver = true

                // opponentRoutes absent - the field did not exist at version 1.
            };

            GameSettingsData result = GameSettingsData.Normalized(stored);

            Assert.AreEqual(0.1f, result.masterVolume, "master");
            Assert.AreEqual(0.2f, result.musicVolume, "music");
            Assert.AreEqual(0.3f, result.effectsVolume, "effects");
            Assert.IsFalse(result.colourblindMarks, "colourblind");
            Assert.IsTrue(result.reducedMotion, "reduced motion");
            Assert.AreEqual(2, result.interfaceSize, "interface size");
            Assert.AreEqual(0.9f, result.cameraSpeed, "camera speed");
            Assert.IsTrue(result.confirmEachCommand, "confirm");
            Assert.IsFalse(result.haptics, "haptics");
            Assert.IsTrue(result.batterySaver, "battery saver");
        }

        [Test]
        public void Normalized_Version1_GivesTheNewFieldItsDefaultNotItsZero()
        {
            // opponentRoutes defaults to true, but a bool absent from an older
            // save deserialises to false. Reading that as "the player turned
            // routes off" would quietly change what they see in a match.
            GameSettingsData stored = new GameSettingsData { version = 1 };

            Assert.IsTrue(GameSettingsData.Normalized(stored).opponentRoutes);
        }

        [Test]
        public void Normalized_Version1_IsNotAWholesaleReset()
        {
            // Guards the specific regression of migrating by returning
            // CreateDefault(), which passes a "defaults are sane" test while
            // wiping every value the player chose.
            GameSettingsData stored = new GameSettingsData
            {
                version = 1,
                masterVolume = 0f,
                interfaceSize = 0
            };

            GameSettingsData result = GameSettingsData.Normalized(stored);
            GameSettingsData defaults = GameSettingsData.CreateDefault();

            Assert.AreNotEqual(defaults.masterVolume, result.masterVolume);
            Assert.AreNotEqual(defaults.interfaceSize, result.interfaceSize);
        }

        [Test]
        public void Normalized_StampsCurrentVersion()
        {
            GameSettingsData result = GameSettingsData.Normalized(new GameSettingsData());

            Assert.AreEqual(GameSettingsData.CurrentVersion, result.version);
        }

        [Test]
        public void CreateDefault_SurvivesNormalizedUnchanged()
        {
            // If this fails, a default sits outside its own clamp.
            GameSettingsData defaults = GameSettingsData.CreateDefault();

            Assert.AreEqual(defaults, GameSettingsData.Normalized(defaults));
        }

        // ===== CLAMPING =====

        [TestCase(-5f, 0f)]
        [TestCase(-0.0001f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(1.0001f, 1f)]
        [TestCase(42f, 1f)]
        public void Normalized_ClampsSlidersIntoRange(float stored, float expected)
        {
            GameSettingsData source = GameSettingsData.CreateDefault();
            source.masterVolume = stored;
            source.musicVolume = stored;
            source.effectsVolume = stored;
            source.cameraSpeed = stored;

            GameSettingsData result = GameSettingsData.Normalized(source);

            Assert.AreEqual(expected, result.masterVolume, "master");
            Assert.AreEqual(expected, result.musicVolume, "music");
            Assert.AreEqual(expected, result.effectsVolume, "effects");
            Assert.AreEqual(expected, result.cameraSpeed, "camera");
        }

        [Test]
        public void Normalized_NaNSlider_BecomesZero()
        {
            // NaN fails every comparison, so a naive clamp passes it straight
            // through to a Slider, which then renders nowhere in particular.
            GameSettingsData source = GameSettingsData.CreateDefault();
            source.masterVolume = float.NaN;

            GameSettingsData result = GameSettingsData.Normalized(source);

            Assert.AreEqual(0f, result.masterVolume);
        }

        [TestCase(-3, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, GameSettingsData.InterfaceSizeCount - 1)]
        [TestCase(99, GameSettingsData.InterfaceSizeCount - 1)]
        public void Normalized_ClampsInterfaceSize(int stored, int expected)
        {
            GameSettingsData source = GameSettingsData.CreateDefault();
            source.interfaceSize = stored;

            Assert.AreEqual(expected, GameSettingsData.Normalized(source).interfaceSize);
        }

        [Test]
        public void DefaultInterfaceSize_IsInsideItsOwnRange()
        {
            Assert.GreaterOrEqual(GameSettingsData.DefaultInterfaceSize, 0);
            Assert.Less(GameSettingsData.DefaultInterfaceSize, GameSettingsData.InterfaceSizeCount);
        }

        // ===== THE SIZE ROW =====

        [Test]
        public void NextInterfaceSize_WrapsForward()
        {
            Assert.AreEqual(1, GameSettingsData.NextInterfaceSize(0));
            Assert.AreEqual(2, GameSettingsData.NextInterfaceSize(1));
            Assert.AreEqual(0, GameSettingsData.NextInterfaceSize(2));
        }

        [Test]
        public void NextInterfaceSize_CyclesEveryEntryAndReturns()
        {
            int index = 0;
            for (int i = 0; i < GameSettingsData.InterfaceSizeCount; i++)
                index = GameSettingsData.NextInterfaceSize(index);

            Assert.AreEqual(0, index, "a full cycle should return to where it started");
        }

        [TestCase(-1)]
        [TestCase(99)]
        public void NextInterfaceSize_ClampsBeforeStepping(int stored)
        {
            int next = GameSettingsData.NextInterfaceSize(stored);

            Assert.GreaterOrEqual(next, 0);
            Assert.Less(next, GameSettingsData.InterfaceSizeCount);
        }

        // ===== CHANGE DETECTION =====

        [Test]
        public void Differ_IsFalseForTheSameValues()
        {
            Assert.IsFalse(GameSettingsData.Differ(
                GameSettingsData.CreateDefault(),
                GameSettingsData.CreateDefault()));
        }

        [Test]
        public void Differ_IgnoresVersionAlone()
        {
            // Version is bookkeeping, not something a player set. A bump on its
            // own must not be mistaken for an edit and written back.
            GameSettingsData a = GameSettingsData.CreateDefault();
            GameSettingsData b = GameSettingsData.CreateDefault();
            b.version = a.version + 1;

            Assert.IsFalse(GameSettingsData.Differ(a, b));
        }

        /// <summary>
        /// Every player-settable field must be one Differ looks at. A field
        /// added to the struct and forgotten here would make its change a
        /// silent no-save - the settings equivalent of a field missing from
        /// SimulationStateHasher, and just as quiet. Reflection rather than a
        /// written-out list, so adding a field is what fails the test.
        /// </summary>
        [Test]
        public void Differ_DetectsEveryPlayerSettableField()
        {
            FieldInfo[] fields = typeof(GameSettingsData)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotEmpty(fields);
            int checkedCount = 0;

            foreach (FieldInfo field in fields)
            {
                if (field.Name == nameof(GameSettingsData.version)) continue;

                object baseline = GameSettingsData.CreateDefault();
                object mutated = GameSettingsData.CreateDefault();

                if (field.FieldType == typeof(float))
                    field.SetValue(mutated, (float)field.GetValue(mutated) + 0.25f);
                else if (field.FieldType == typeof(int))
                    field.SetValue(mutated, (int)field.GetValue(mutated) + 1);
                else if (field.FieldType == typeof(bool))
                    field.SetValue(mutated, !(bool)field.GetValue(mutated));
                else
                    Assert.Fail("Differ_DetectsEveryPlayerSettableField cannot mutate " +
                                field.Name + " of type " + field.FieldType.Name +
                                "; teach it that type.");

                Assert.IsTrue(
                    GameSettingsData.Differ((GameSettingsData)baseline, (GameSettingsData)mutated),
                    "Differ missed a change to " + field.Name);

                checkedCount++;
            }

            Assert.Greater(checkedCount, 0, "no settable fields were exercised");
        }
    }
}
