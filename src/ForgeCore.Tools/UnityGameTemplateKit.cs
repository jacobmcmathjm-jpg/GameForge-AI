using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ForgeCore.Tools;

internal static partial class UnityGameTemplateKit
{
    public const string Version = "0.16.33";
    private const string Root = "Assets/ForgeCoreGameTemplates";
    private const string ManifestName = "forgecore-game-templates.manifest.json";
    private static readonly IReadOnlySet<string> Templates = new HashSet<string>(
        ["first_person_action", "platformer", "top_down_rpg", "racing"], StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Runtime/PrototypeTypes.cs"] = PrototypeTypesSource,
        ["Runtime/PrototypeGame.cs"] = PrototypeGameSource,
        ["Runtime/PrototypeCampaignProgress.cs"] = PrototypeCampaignProgressSource,
        ["Runtime/PrototypeFeedback.cs"] = PrototypeFeedbackSource,
        ["Runtime/PrototypeSurfaceLibrary.cs"] = PrototypeSurfaceLibrarySource,
        ["Runtime/PrototypeArtDirection.cs"] = PrototypeArtDirectionSource,
        ["Runtime/PrototypeCombatAnimation.cs"] = CombatAnimationSource,
        ["Runtime/PrototypeProjectile.cs"] = ProjectileSource,
        ["Runtime/PrototypeRangedEnemy.cs"] = RangedEnemySource,
        ["Runtime/PrototypeActor.cs"] = PrototypeActorSource,
        ["Runtime/PrototypeObjective.cs"] = PrototypeObjectiveSource,
        ["Runtime/PrototypeCamera.cs"] = PrototypeCameraSource,
        ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = RuntimeAssembly,
        ["Editor/ForgeCorePrototypeBuilder.cs"] = EditorSource,
        ["Editor/ForgeCoreIndustrialYard.cs"] = IndustrialYardSource,
        ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = EditorAssembly,
        ["Tests/EditMode/PrototypeCampaignTests.cs"] = CampaignTestsSource,
        ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = TestAssembly,
        ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = RuntimeInputTestsSource,
        ["Tests/PlayMode/PrototypeCombatTests.cs"] = CombatTestsSource,
        ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = PlayModeTestAssembly
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> UpgradeableInstallations =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
    {
        ["0.16.32"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "7324e949236249393e6b1d13335696dd21c0265078a07b81753e02a42013dbb1",
            ["Runtime/PrototypeGame.cs"] = "c8ff96d3ccd7d998373bb838c521b96b5116ae6aef690ea4139eac71ab9a6b3f",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeArtDirection.cs"] = "c2a8afd7f5ddaf069a8cd45bf95caaa7c1ceaae8fdb3d3e560bb95ee6a6be928",
            ["Runtime/PrototypeCombatAnimation.cs"] = "0320c8d84c73a73abb68019a508f0c39d793c63421824ac4a7ade1a5785dd0af",
            ["Runtime/PrototypeProjectile.cs"] = "3b8f2b6097d19b6fb84a1f15df0d7432237a253a2cd6e9088dcb9ddab29f3929",
            ["Runtime/PrototypeRangedEnemy.cs"] = "2ce65cdbfc469cab06ae29bc47ad5f35af154af451e48210c529522e4bb9f739",
            ["Runtime/PrototypeActor.cs"] = "85c792ff046f78749a042343f59ebf33076afb3a9ea60b64367a09be55df7bc7",
            ["Runtime/PrototypeObjective.cs"] = "319224259724a86bc5aa2b78f30a78f86f41ab4dfcd190dbd2a4a7f5c8b14085",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "85d1c51e1d18e71a4dcde1e7bf961fcb16d7dc6b0e7b2c1b7c1a136d905e663f",
            ["Editor/ForgeCoreIndustrialYard.cs"] = "78cc75653f80f164acd1f4dc312e51c51631978700e85794afd545408bd356b8",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "038d738e8827abec147b890fb23e9c230cb809dfeb1f16724ff0eb9b7a102a47",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/PrototypeCombatTests.cs"] = "ba339c9de3e2c4c733ee6ecd9a8e103afd76c7aefe8d47fc1b6dac5bbe28f14d",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7",
        },
        ["0.16.31"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "7324e949236249393e6b1d13335696dd21c0265078a07b81753e02a42013dbb1",
            ["Runtime/PrototypeGame.cs"] = "ec362139364230b4a8884ddd2f87853d34c701f697b150f386382452e675e383",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeArtDirection.cs"] = "c2a8afd7f5ddaf069a8cd45bf95caaa7c1ceaae8fdb3d3e560bb95ee6a6be928",
            ["Runtime/PrototypeCombatAnimation.cs"] = "0320c8d84c73a73abb68019a508f0c39d793c63421824ac4a7ade1a5785dd0af",
            ["Runtime/PrototypeProjectile.cs"] = "ff0e028122a13362c9c58f3b0b464fa5639de9cc74e2b7c96a24f8cc6e770343",
            ["Runtime/PrototypeRangedEnemy.cs"] = "7f9406aa6a57e3e1b5525b5989b05b6562562bc55b9ed475b690635a60a65177",
            ["Runtime/PrototypeActor.cs"] = "520e5110075143758b871180c784fec241af89e6bc5951a7cc0b91a6bfa5554c",
            ["Runtime/PrototypeObjective.cs"] = "319224259724a86bc5aa2b78f30a78f86f41ab4dfcd190dbd2a4a7f5c8b14085",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "85d1c51e1d18e71a4dcde1e7bf961fcb16d7dc6b0e7b2c1b7c1a136d905e663f",
            ["Editor/ForgeCoreIndustrialYard.cs"] = "78cc75653f80f164acd1f4dc312e51c51631978700e85794afd545408bd356b8",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "038d738e8827abec147b890fb23e9c230cb809dfeb1f16724ff0eb9b7a102a47",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/PrototypeCombatTests.cs"] = "ba339c9de3e2c4c733ee6ecd9a8e103afd76c7aefe8d47fc1b6dac5bbe28f14d",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.30"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "7324e949236249393e6b1d13335696dd21c0265078a07b81753e02a42013dbb1",
            ["Runtime/PrototypeGame.cs"] = "e7b8c74e078ab3eeb4f619fe1f3de8fbb4c544fdbd2135457986a6aa32c45f25",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeArtDirection.cs"] = "c2a8afd7f5ddaf069a8cd45bf95caaa7c1ceaae8fdb3d3e560bb95ee6a6be928",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "5a8f42c5328a5fa51c6540419ae14e97f35e826c3e4940c4012ed2b6b5c09748",
            ["Editor/ForgeCoreIndustrialYard.cs"] = "cc038abe8d89bf699fecf0703190deab6a9125b8cb1b6e496a3a22dd6f512802",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "28d65cfe4a7f347b05b41b4e968b3d67afc61b9c9817983277ea2ca576ca98e7",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.29"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "7324e949236249393e6b1d13335696dd21c0265078a07b81753e02a42013dbb1",
            ["Runtime/PrototypeGame.cs"] = "95b0bca4c60e34aaef99aeeb2acd141eabb973a756a76e3045a128325872bd34",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.0"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/ForgeCorePrototype.cs"] = "84302b51327c280b4d02167fc780da54f9f3c6efb6b5f9679f5a7682bcdc2e6c",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "15b2f3742ebc95d1201303249c14d39e8ae917519c31a3e3b6729b4cf095f490",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd"
        },
        ["0.16.1"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "1ad7ec8b47b6010f439384eca55efc9236a153efc14f91f95aeeb76f3df4210e",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "15b2f3742ebc95d1201303249c14d39e8ae917519c31a3e3b6729b4cf095f490",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd"
        },
        ["0.16.2"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "1ad7ec8b47b6010f439384eca55efc9236a153efc14f91f95aeeb76f3df4210e",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "fd8aa3a35d54820dfb68005baa0ece726a54f72d7f778d9bd234e9b212bbdce5",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd"
        },
        ["0.16.3"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "1ad7ec8b47b6010f439384eca55efc9236a153efc14f91f95aeeb76f3df4210e",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "f5a58cde1994ad9a07458ac2de7418bac30691c99004a3803e53113191d9748e",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd"
        },
        ["0.16.4"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "462ac7df0a9c15392b4d20dc5e46c9053333ce747ce0a1cf0e52073c6e755782",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "e7df617fb3637ae26208141ebe84ad2ac22a19d9ebec7204afe5fa7a403b0957",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd"
        },
        ["0.16.5"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "462ac7df0a9c15392b4d20dc5e46c9053333ce747ce0a1cf0e52073c6e755782",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "26abbda9ebf980f6af039f252ad87efb9423952d3a26283f13a38d197a23d9fe",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd"
        },
        ["0.16.6"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "4071cbf9fc2cbe01dcd4cada045e15cbc441292438c452407c5f14cc8a863e1b",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "26abbda9ebf980f6af039f252ad87efb9423952d3a26283f13a38d197a23d9fe",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.7"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "11ac46a9948a0cf59bc154145ee65e16976c024d96c011258fcc831f748b41a3",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "26abbda9ebf980f6af039f252ad87efb9423952d3a26283f13a38d197a23d9fe",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.8"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "c81622ab4ae5a43b3f1e6f5793dd9e97a765320c3c12502a97d2c200c496bf14",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "e7b2b0fa3ea62c3fdb83e746aac6ba11a7703a80a3ea40378595414607e5c180",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "26abbda9ebf980f6af039f252ad87efb9423952d3a26283f13a38d197a23d9fe",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.9"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "c81622ab4ae5a43b3f1e6f5793dd9e97a765320c3c12502a97d2c200c496bf14",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "26abbda9ebf980f6af039f252ad87efb9423952d3a26283f13a38d197a23d9fe",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.10"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "b3ec28bbe6505e50078cf10015f42e7d9ae24c4b43a0c8c97e4b0fc7b2ccc1d4",
            ["Runtime/PrototypeActor.cs"] = "4ab397cd669e3f8961859eb38d23fe2b255113de6757ba34f4cf98d792c3ef92",
            ["Runtime/PrototypeObjective.cs"] = "74a77c68410477d585a88ecf2c7b96ecb108952dab31f0053b166832da093c14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.11"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "0c65bfc8dc44159592422e81e04649a76ff5cd2dcf0fe32edee7bea65cd4aef8",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "0df7e4a207cf9b02bcfd480d4aa1557792dab5efb39da8bb6aa4f4275a80559e",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.12"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "61477be73a4d678ea800a4d67439c2e7ef6744cb2ae3f108ee98a6df1ab2b7a5",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "0df7e4a207cf9b02bcfd480d4aa1557792dab5efb39da8bb6aa4f4275a80559e",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "51461de6ef749794591ae56526530a6dbe16d8e83deedc594f91e262941517b6",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.13"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "163fd186802c15a74be1653c8e6a940f9d55830c48c17deb86aecb93e6789881",
            ["Runtime/PrototypeCampaignProgress.cs"] = "05496f03c378fe84ef928d2d8085f11b81c1eb6875a18bceeb5d7cdfe307b993",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "0df7e4a207cf9b02bcfd480d4aa1557792dab5efb39da8bb6aa4f4275a80559e",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "83e24b33c2431d65ed3ab643bbc4efb6699a34fd90612f218fd20e0afaa89abe",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.14"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "163fd186802c15a74be1653c8e6a940f9d55830c48c17deb86aecb93e6789881",
            ["Runtime/PrototypeCampaignProgress.cs"] = "05496f03c378fe84ef928d2d8085f11b81c1eb6875a18bceeb5d7cdfe307b993",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "cf862e2d4c865759dc10f491f01c896c90b4d51760fca06efac4e1d184b9d03d",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "50d5dfb6a1848275928d245bb4b3c173e1e7a5e352d1cff6eb9a9256d2813622",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.15"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "163fd186802c15a74be1653c8e6a940f9d55830c48c17deb86aecb93e6789881",
            ["Runtime/PrototypeCampaignProgress.cs"] = "05496f03c378fe84ef928d2d8085f11b81c1eb6875a18bceeb5d7cdfe307b993",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "cf862e2d4c865759dc10f491f01c896c90b4d51760fca06efac4e1d184b9d03d",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "3e0268ae5a207d386c2893ea2ca9c9d1fbf69d061d170e3df324781c769fb635",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.16"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "e6f6d080396e4f96d74095b600ef4699ad4b493c411cf536cb7f085c30c9476c",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "8be43e637f651b772aaa6d7436c9a4d10e0612f6a529708254adc36e324e3a11",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "7ec5f86c430d202ece799068e4f9be9e333fe5372571dbacd4fbb779f2c26333",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "0a6eeef4dfe260ae3202b670852e56931bc1aa27f5da13613a6d8b6a3082bcc3",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.17"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "e6f6d080396e4f96d74095b600ef4699ad4b493c411cf536cb7f085c30c9476c",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "1382fc90812f343a47ee910ae6c2314e91e8ae6359ac01d64624c7c58119b193",
            ["Runtime/PrototypeActor.cs"] = "a5ab012fb7f62f289b55e17eae0ad1f45ec21e87e2b47cdb950d99cc8cbd6d58",
            ["Runtime/PrototypeObjective.cs"] = "7ec5f86c430d202ece799068e4f9be9e333fe5372571dbacd4fbb779f2c26333",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "013c6500ee1f94b10bf4a8fc52bff8f102fd4ce8097d64509346e40b8112b333",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "0a6eeef4dfe260ae3202b670852e56931bc1aa27f5da13613a6d8b6a3082bcc3",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.18"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "bd237e1e71df59fa8fa937559198eab3d9650485bcb3c5560d25fe5dc0fe884b",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "c6a45a9a8eccbd4b0f5e56e065fa6709bf14ba05ce6dfde52c96f01552e2a13f",
            ["Runtime/PrototypeActor.cs"] = "67fe52444ae2c58b5b7e16f11a3f8512e2022aafe13548f96307c47a6d84f014",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "54c34a86b21761d8a3e1a0a52fe8d0a68fa9931918fd07ccb6c24c3f85a83cf1",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "c79f64415873edb2a9d04eea545a922dd1246c2176a41e5a63b67bb0765837db",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.19"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "c9987f1b87c5865a79831fdb25d7bee1a0bb655ffde4eceb248951e22dc80a1b",
            ["Runtime/PrototypeGame.cs"] = "bd237e1e71df59fa8fa937559198eab3d9650485bcb3c5560d25fe5dc0fe884b",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "c6a45a9a8eccbd4b0f5e56e065fa6709bf14ba05ce6dfde52c96f01552e2a13f",
            ["Runtime/PrototypeActor.cs"] = "67fe52444ae2c58b5b7e16f11a3f8512e2022aafe13548f96307c47a6d84f014",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "c79f64415873edb2a9d04eea545a922dd1246c2176a41e5a63b67bb0765837db",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.20"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "7e88aaa11cb72242eac4fdb9f6b36c23954366aee0fa94d43f43263c0901d8f6",
            ["Runtime/PrototypeGame.cs"] = "188ebcc1d5b6650ca986003e7e78cd8e98dcda9d7ea4acb0c1bf449081e352eb",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "c6a45a9a8eccbd4b0f5e56e065fa6709bf14ba05ce6dfde52c96f01552e2a13f",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "67fe52444ae2c58b5b7e16f11a3f8512e2022aafe13548f96307c47a6d84f014",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "ca0f9bfe5e329510070f763b4b91015f5697330db8ed6c716212b8863f07a6df",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc"
        },
        ["0.16.21"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "d7990512545ee5534c9bec6548632e5e70db29eaee54c9726d1c1089f7986946",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "c6a45a9a8eccbd4b0f5e56e065fa6709bf14ba05ce6dfde52c96f01552e2a13f",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "1e5e29359a082947ecf5095dd4596cf8845dbd8cd00f3cbda5467c8c600e78ab",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.22"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "d7990512545ee5534c9bec6548632e5e70db29eaee54c9726d1c1089f7986946",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "1e5e29359a082947ecf5095dd4596cf8845dbd8cd00f3cbda5467c8c600e78ab",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.23"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "d7990512545ee5534c9bec6548632e5e70db29eaee54c9726d1c1089f7986946",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "9cf0ae0342cf05762af4c683bad0c4bba215fe772153af1c871cc85469cff9f9",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.24"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "d7990512545ee5534c9bec6548632e5e70db29eaee54c9726d1c1089f7986946",
            ["Runtime/PrototypeCampaignProgress.cs"] = "0c26c00f74c93700b08db8b5a773c4d4de95701055c659e98fb8a5d22b1c6671",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "544fb4cc43c78a4ac6beac553e726cdc7d6e934730a90362a052e2ca7612afc3",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.25"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0d1ec2783213c4294bba2ad6c6bedf781b63b36464d0685587cde6c6b9b33559",
            ["Runtime/PrototypeGame.cs"] = "a9649b53d4eccf5faff48b0909c5fbb04a9454f8353ccdf26a0b2ea7fb41a8d2",
            ["Runtime/PrototypeCampaignProgress.cs"] = "d92f757356b37274588aff58ec12877e043a28e0da450fb38dd9bad257a7ff98",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.26"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "a9649b53d4eccf5faff48b0909c5fbb04a9454f8353ccdf26a0b2ea7fb41a8d2",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.27"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "36b13508aa41101e375e57b4d0992ef6c26406652453cf57b81a9474fe5847f2",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        },
        ["0.16.28"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime/PrototypeTypes.cs"] = "0fc9034bc9becd61cf04d2aee9475366911ac11147b6dc9db955921e5ef778b1",
            ["Runtime/PrototypeGame.cs"] = "66f8801017f0839834eb2c471f295e9a08c41cb94b33302ab3e3173e4bd3e69c",
            ["Runtime/PrototypeCampaignProgress.cs"] = "6990b77e1f49dda3694ba9232eb12942cbf9e9a2dd69f48618447d199a0d43e2",
            ["Runtime/PrototypeFeedback.cs"] = "51ac0ce307105fd934ffb20a3c05296a84bda32ba31917253ba4d6ae94f7eaeb",
            ["Runtime/PrototypeSurfaceLibrary.cs"] = "4157fd39617674204d5e42f98b4b8bfad1bee938e325cc1ceb1dd06400577e41",
            ["Runtime/PrototypeActor.cs"] = "28e6670a362a945d34a619584f8ce6ccb6248fd3ee253da37b98f0aaa8fb8b1a",
            ["Runtime/PrototypeObjective.cs"] = "cdc93330fdbe389505d49d9f3304eaeaa558beb561e8fe9e07613864388e7d14",
            ["Runtime/PrototypeCamera.cs"] = "870c3636dba0f067e3bac1a6fc135909032cf58c6579b21a0890aa0ef0357225",
            ["Runtime/ForgeCoreGameTemplates.Runtime.asmdef"] = "b6961decb8e4dc3ee09daeaaddf53d24314cf9a0e8bd0aa7386fcea47505a02b",
            ["Editor/ForgeCorePrototypeBuilder.cs"] = "6487b9dbf9c16c3a137b7e3f743c8d925c8f3c67551dfc46818ac2d973cc4781",
            ["Editor/ForgeCoreGameTemplates.Editor.asmdef"] = "66f7b1d0799e5e578efc058e33d9e3b09e5e6840b08331db3c1e9617195e08cd",
            ["Tests/EditMode/PrototypeCampaignTests.cs"] = "d0964b07ee8b1ffddd6774b5059131cd84120d628ed6354ae674fb5874f684c2",
            ["Tests/EditMode/ForgeCoreGameTemplates.Tests.EditMode.asmdef"] = "d8b6caa6c78f3a075440445563b80c8dddf7067e3ecb1aee336b6ca084461ffc",
            ["Tests/PlayMode/PrototypeRuntimeInputTests.cs"] = "4b6d897beec905750678a51c64833e697e8547176ece56d588ff925f4f0ee606",
            ["Tests/PlayMode/ForgeCoreGameTemplates.Tests.PlayMode.asmdef"] = "0516b9d7917f4a29f6ad7cc2368c096782e9d7eb1d4acb15e802029b635aaec7"
        }
    };

    public static async Task<string> InstallAsync(string projectRoot, string templateId, CancellationToken token)
    {
        if (!Templates.Contains(templateId)) throw new ArgumentException("Unsupported Unity game template.", nameof(templateId));
        var root = Path.Combine(projectRoot, Root.Replace('/', Path.DirectorySeparatorChar));
        var manifest = Path.Combine(root, ManifestName);
        if (Directory.Exists(root))
        {
            if (await InstallationMatchesAsync(projectRoot, token))
                return $"ForgeCore game-template kit {Version} is already installed and hash-verified; '{templateId}' is available.";
            var upgradeableVersion = await UpgradeableInstallationVersionAsync(projectRoot, token);
            if (upgradeableVersion is not null)
                return await UpgradeAsync(projectRoot, root, templateId, upgradeableVersion, token);
            throw new InvalidOperationException("A ForgeCore game-template directory already exists and does not exactly match this release; it was not overwritten.");
        }

        await WriteInstallationAsync(root, token);
        return $"Installed hash-verified ForgeCore game-template kit {Version} with {Files.Count} source files; '{templateId}' is ready for approved scene creation.";
    }

    private static async Task WriteInstallationAsync(string root, CancellationToken token)
    {
        var manifest = Path.Combine(root, ManifestName);
        var created = new List<string>();
        try
        {
            foreach (var (relative, content) in Files)
            {
                token.ThrowIfCancellationRequested();
                var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, content, new UTF8Encoding(false), token);
                created.Add(target);
            }
            var manifestText = JsonSerializer.Serialize(new
            {
                version = Version,
                templates = Templates.Order(StringComparer.Ordinal).ToArray(),
                files = Files.Select(x => new { path = x.Key, sha256 = Hash(x.Value) }).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifest, manifestText, new UTF8Encoding(false), token);
            created.Add(manifest);
        }
        catch
        {
            foreach (var file in created.AsEnumerable().Reverse()) if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Any()) Directory.Delete(root, true);
            throw;
        }
    }

    private static async Task<string> UpgradeAsync(string projectRoot, string root, string templateId, string priorVersion, CancellationToken token)
    {
        var backup = Path.Combine(projectRoot, ".forgecore", "unity", "template-kit-backups", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        Directory.Move(root, backup);
        try
        {
            await WriteInstallationAsync(root, token);
        }
        catch
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.Move(backup, root);
            throw;
        }
        return $"Upgraded hash-verified ForgeCore game-template kit {priorVersion} to {Version}; protected backup: {backup}; '{templateId}' is available.";
    }

    private static async Task<string?> UpgradeableInstallationVersionAsync(string projectRoot, CancellationToken token)
    {
        var root = Path.Combine(projectRoot, Root.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Path.Combine(root, ManifestName);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, token));
            var value = document.RootElement;
            var version = value.GetProperty("version").GetString();
            if (version is null || !UpgradeableInstallations.TryGetValue(version, out var upgradeableFiles)) return null;
            var templates = value.GetProperty("templates").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            if (!templates.SetEquals(Templates)) return null;
            var listed = value.GetProperty("files").EnumerateArray().ToDictionary(
                x => x.GetProperty("path").GetString()!, x => x.GetProperty("sha256").GetString()!, StringComparer.Ordinal);
            if (listed.Count != upgradeableFiles.Count) return null;
            foreach (var (relative, expectedHash) in upgradeableFiles)
            {
                token.ThrowIfCancellationRequested();
                if (!listed.TryGetValue(relative, out var listedHash) || !string.Equals(listedHash, expectedHash, StringComparison.Ordinal)) return null;
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return null;
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var actual = await SHA256.HashDataAsync(stream, token);
                if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedHash))) return null;
            }
            return version;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return null;
        }
    }

    public static async Task<bool> InstallationMatchesAsync(string projectRoot, CancellationToken token)
    {
        var root = Path.Combine(projectRoot, Root.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Path.Combine(root, ManifestName);
        if (!File.Exists(manifestPath)) return false;
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, token));
            var value = document.RootElement;
            if (value.GetProperty("version").GetString() != Version) return false;
            var templates = value.GetProperty("templates").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            if (!templates.SetEquals(Templates)) return false;
            var listed = value.GetProperty("files").EnumerateArray().ToDictionary(
                x => x.GetProperty("path").GetString()!, x => x.GetProperty("sha256").GetString()!, StringComparer.Ordinal);
            if (listed.Count != Files.Count) return false;
            foreach (var (relative, expectedContent) in Files)
            {
                token.ThrowIfCancellationRequested();
                if (!listed.TryGetValue(relative, out var listedHash) || !string.Equals(listedHash, Hash(expectedContent), StringComparison.Ordinal)) return false;
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return false;
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var actual = await SHA256.HashDataAsync(stream, token);
                if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(listedHash))) return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private const string RuntimeAssembly = """
{
  "name": "ForgeCoreGameTemplates.Runtime",
  "rootNamespace": "ForgeCoreGameTemplates",
  "autoReferenced": true
}
""";

    private const string EditorAssembly = """
{
  "name": "ForgeCoreGameTemplates.Editor",
  "rootNamespace": "ForgeCoreGameTemplates.Editor",
  "references": ["ForgeCoreGameTemplates.Runtime"],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
""";

    private const string TestAssembly = """
{
  "name": "ForgeCoreGameTemplates.Tests.EditMode",
  "rootNamespace": "ForgeCoreGameTemplates.Tests",
  "references": ["ForgeCoreGameTemplates.Runtime"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
""";

    private const string PlayModeTestAssembly = """
{
  "name": "ForgeCoreGameTemplates.Tests.PlayMode",
  "rootNamespace": "ForgeCoreGameTemplates.Tests",
  "references": ["ForgeCoreGameTemplates.Runtime"],
  "autoReferenced": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
""";

    private const string CampaignTestsSource = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ForgeCoreGameTemplates;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeCoreGameTemplates.Tests
{
    public sealed class PrototypeCampaignTests
    {
        [Test]
        public void SavedPrototypeScenes_ReloadWithCoreGameplayReferences()
        {
            var paths = PrototypeScenePaths();
            if (paths.Count == 0) Assert.Ignore("No generated ForgeCore prototype scenes exist yet.");
            foreach (var path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var games = ComponentsInScene<PrototypeGame>(scene); var actors = ComponentsInScene<PrototypeActor>(scene);
                var objectives = ComponentsInScene<PrototypeObjective>(scene).Where(item => item.kind != ObjectiveKind.Hazard).ToArray();
                Assert.That(games, Has.Length.EqualTo(1), path + " must contain one PrototypeGame.");
                Assert.That(actors, Has.Length.EqualTo(1), path + " must contain one PrototypeActor.");
                Assert.That(games[0].player, Is.SameAs(actors[0]), path + " lost its serialized player reference.");
                Assert.That(objectives, Has.Length.EqualTo(games[0].requiredObjectives), path + " objective count changed after serialization.");
                Assert.That(ComponentsInScene<Camera>(scene), Has.Some.Matches<Camera>(camera => camera.CompareTag("MainCamera")), path + " needs a MainCamera.");
            }
        }

        [Test]
        public void SavedCampaigns_HaveContiguousLevelsAndExistingTransitions()
        {
            var campaigns = CampaignScenes();
            if (campaigns.Count == 0) Assert.Ignore("No generated ForgeCore campaigns exist yet.");
            foreach (var item in campaigns)
            {
                var scene = EditorSceneManager.OpenScene(item.Path, OpenSceneMode.Single);
                var game = ComponentsInScene<PrototypeGame>(scene).Single();
                Assert.That(game.levelNumber, Is.EqualTo(item.Level), item.Path + " has the wrong serialized level number.");
                Assert.That(game.levelCount, Is.EqualTo(item.Count), item.Path + " has the wrong serialized campaign size.");
                var expectedNext = item.Level < item.Count ? CampaignPath(item.Path, item.Level + 1) : "";
                Assert.That(game.nextScenePath, Is.EqualTo(expectedNext), item.Path + " has the wrong completion transition.");
                if (expectedNext.Length > 0) Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(expectedNext), Is.Not.Null, expectedNext + " is missing.");
            }
        }

        [Test]
        public void IndustrialYardScenes_KeepSolidBoundariesHitCollidersAndNonCollidingViewModels()
        {
            foreach (var path in PrototypeScenePaths())
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var art = ComponentsInScene<PrototypeArtDirection>(scene);
                if (art.Length == 0) continue; // Legacy scenes keep their original art.
                Assert.That(art, Has.Length.EqualTo(1));
                var game = ComponentsInScene<PrototypeGame>(scene).Single();
                Assert.That(game.templateId, Is.EqualTo("first_person_action"));
                Assert.That(game.player.weaponView.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThanOrEqualTo(12));
                Assert.That(game.player.weaponView.GetComponentsInChildren<Collider>(true), Is.Empty);
                var targets = ComponentsInScene<PrototypeObjective>(scene);
                Assert.That(targets, Has.Length.EqualTo(6));
                foreach (var target in targets)
                {
                    Assert.That(target.GetComponent<Collider>(), Is.Not.Null);
                    Assert.That(target.GetComponent<Renderer>().enabled, Is.False);
                    Assert.That(target.GetComponentsInChildren<Renderer>().Length, Is.GreaterThanOrEqualTo(8));
                    if (target.GetComponent<PrototypeRangedEnemy>() != null)
                    {
                        var pose = target.GetComponentInChildren<PrototypeCombatAnimation>();
                        Assert.That(pose, Is.Not.Null); Assert.That(pose.Muzzle, Is.Not.Null);
                        Assert.That(pose.transform.Find("LeftHip/Knee/Foot"), Is.Not.Null);
                        Assert.That(pose.transform.Find("RightHip/Knee/Foot"), Is.Not.Null);
                        Assert.That(game.player.weaponView.GetComponentInChildren<PrototypeCombatAnimation>(), Is.Not.Null);
                    }
                }
                var boundaryCount = ComponentsInScene<BoxCollider>(scene).Count(item => item.name.Contains("boundary"));
                Assert.That(boundaryCount, Is.EqualTo(4));
            }
        }

        [Test]
        public void CampaignProgressSlots_AreStableAcrossLevelsAndSeparatedByCampaign()
        {
            var first = PrototypeCampaignProgress.SlotFor("first_person_action", "FrontierCampaign-Level01");
            var third = PrototypeCampaignProgress.SlotFor("first_person_action", "FrontierCampaign-Level03");
            Assert.That(third, Is.EqualTo(first));
            Assert.That(PrototypeCampaignProgress.SlotFor("racing", "FrontierCampaign-Level01"), Is.Not.EqualTo(first));
            Assert.That(PrototypeCampaignProgress.SlotFor("first_person_action", "OtherCampaign-Level01"), Is.Not.EqualTo(first));
            Assert.That(first, Does.StartWith("ForgeCore.Progress.v1."));
        }

        [Test]
        public void CampaignLevelSelection_DerivesOnlyBoundedCampaignPaths()
        {
            const string first = "Assets/Scenes/Campaigns/FrontierCampaign-Level01.unity";
            Assert.That(PrototypeCampaignProgress.ScenePathForLevel(first, 3, 3),
                Is.EqualTo("Assets/Scenes/Campaigns/FrontierCampaign-Level03.unity"));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrototypeCampaignProgress.ScenePathForLevel(first, 4, 3));
            Assert.Throws<InvalidOperationException>(() => PrototypeCampaignProgress.ScenePathForLevel("Assets/Scenes/Prototype.unity", 2, 3));
        }

        [Test]
        public void ScriptedControlFrame_ClampsAnalogueAxes()
        {
            var frame = new PrototypeInputFrame(moveX: 8f, moveY: -4f, lookX: 3f, lookY: -9f);
            Assert.That(frame.MoveX, Is.EqualTo(1f)); Assert.That(frame.MoveY, Is.EqualTo(-1f));
            Assert.That(frame.LookX, Is.EqualTo(1f)); Assert.That(frame.LookY, Is.EqualTo(-1f));
        }

        [Test]
        public void ProceduralFeedbackTone_IsShortBoundedAndMono()
        {
            var clip = PrototypeFeedback.CreateToneForVerification();
            try
            {
                Assert.That(clip.samples, Is.InRange(256, 22050));
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(22050));
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
        }

        [Test]
        public void ProceduralSurfaces_AreDeterministicAndDistinct()
        {
            var first = PrototypeSurfaceLibrary.CreateForVerification(SurfaceKind.Structure, 3);
            var second = PrototypeSurfaceLibrary.CreateForVerification(SurfaceKind.Structure, 3);
            var ground = PrototypeSurfaceLibrary.CreateForVerification(SurfaceKind.Ground, 3);
            try
            {
                Assert.That(first.width, Is.EqualTo(64)); Assert.That(first.height, Is.EqualTo(64));
                Assert.That(first.GetPixels32().SequenceEqual(second.GetPixels32()), Is.True);
                Assert.That(first.GetPixels32().SequenceEqual(ground.GetPixels32()), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); UnityEngine.Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void FirstPersonWeaponState_UsesBoundedMagazineAndReload()
        {
            var actorObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                UnityEngine.Object.DestroyImmediate(actorObject.GetComponent<Collider>());
                actorObject.AddComponent<CharacterController>();
                var actor = actorObject.AddComponent<PrototypeActor>(); actor.mode = PrototypeMode.FirstPersonAction;
                typeof(PrototypeActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, null);
                Assert.That(actor.AutomatedWeaponCycleForVerification(), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(actorObject); }
        }

        [Test]
        public void TargetArchetypes_HaveOrderedBoundedCombatProfiles()
        {
            Assert.That(PrototypeObjective.HealthFor(0, 3), Is.LessThan(PrototypeObjective.HealthFor(1, 3)));
            Assert.That(PrototypeObjective.HealthFor(1, 3), Is.LessThan(PrototypeObjective.HealthFor(2, 3)));
            Assert.That(PrototypeObjective.MoveSpeedFor(0, 3), Is.GreaterThan(PrototypeObjective.MoveSpeedFor(1, 3)));
            Assert.That(PrototypeObjective.MoveSpeedFor(1, 3), Is.GreaterThan(PrototypeObjective.MoveSpeedFor(2, 3)));
            Assert.That(PrototypeObjective.ContactDamageFor(0, 3), Is.LessThan(PrototypeObjective.ContactDamageFor(2, 3)));
        }

        [Test]
        public void AutomatedCombatVerification_ConsumesMultiHitTarget()
        {
            var root = new GameObject("Combat test"); var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                var game = root.AddComponent<PrototypeGame>();
                typeof(PrototypeGame).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                game.requiredObjectives = 1; game.templateId = "first_person_action";
                var objective = target.AddComponent<PrototypeObjective>(); objective.kind = ObjectiveKind.Target; objective.health = 4;
                Assert.That(objective.AutomatedInteractForVerification(), Is.True);
                Assert.That(game.Complete, Is.True); Assert.That(game.CompletedObjectives, Is.EqualTo(1));
                Assert.That(target.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SerializedCampaignObjectiveRulesRemainReachableAndOrdered()
        {
            var campaigns = CampaignScenes();
            if (campaigns.Count == 0) Assert.Ignore("No generated ForgeCore campaigns exist yet.");
            foreach (var item in campaigns)
            {
                var scene = EditorSceneManager.OpenScene(item.Path, OpenSceneMode.Single);
                var game = ComponentsInScene<PrototypeGame>(scene).Single();
                var objectives = ComponentsInScene<PrototypeObjective>(scene).Where(value => value.kind != ObjectiveKind.Hazard).OrderBy(value => value.order).ToArray();
                if (game.templateId == "racing" && objectives.Length > 1)
                    Assert.That(game.Register(objectives[1].kind, objectives[1].order), Is.False, item.Path + " accepted an out-of-order checkpoint.");
                for (var index = 0; index < objectives.Length; index++)
                {
                    Assert.That(game.Register(objectives[index].kind, objectives[index].order), Is.True, item.Path + " rejected an in-order objective.");
                    Assert.That(game.Complete, Is.EqualTo(index + 1 == objectives.Length), item.Path + " completion state changed at objective " + index + ".");
                }
                Assert.That(game.CompletedObjectives, Is.EqualTo(game.requiredObjectives));
            }
        }

        private static List<string> PrototypeScenePaths()
        {
            var result = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (ComponentsInScene<PrototypeGame>(scene).Length == 1) result.Add(path);
            }
            result.Sort(StringComparer.Ordinal); return result;
        }

        private static List<CampaignScene> CampaignScenes()
        {
            var candidates = new List<(string Path, string Prefix, int Level)>();
            foreach (var path in PrototypeScenePaths())
            {
                var marker = path.LastIndexOf("-Level", StringComparison.Ordinal); var extension = path.EndsWith(".unity", StringComparison.Ordinal) ? path.Length - 6 : -1;
                if (marker < 0 || extension != marker + 8 || !int.TryParse(path.Substring(marker + 6, 2), out var level)) continue;
                candidates.Add((path, path.Substring(0, marker + 6), level));
            }
            var result = new List<CampaignScene>();
            foreach (var group in candidates.GroupBy(item => item.Prefix, StringComparer.Ordinal))
            {
                var ordered = group.OrderBy(item => item.Level).ToArray();
                if (ordered.Length < 2) continue;
                for (var index = 0; index < ordered.Length; index++)
                {
                    Assert.That(ordered[index].Level, Is.EqualTo(index + 1), group.Key + " campaign numbering is not contiguous.");
                    result.Add(new CampaignScene(ordered[index].Path, ordered[index].Level, ordered.Length));
                }
            }
            return result;
        }

        private static string CampaignPath(string current, int level)
        {
            var marker = current.LastIndexOf("-Level", StringComparison.Ordinal);
            return current.Substring(0, marker + 6) + level.ToString("00") + ".unity";
        }

        private static T[] ComponentsInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result.ToArray();
        }

        private readonly struct CampaignScene
        {
            public readonly string Path; public readonly int Level; public readonly int Count;
            public CampaignScene(string path, int level, int count) { Path = path; Level = level; Count = count; }
        }
    }
}
""";

    private const string RuntimeInputTestsSource = """
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ForgeCoreGameTemplates.Tests
{
    public sealed class PrototypeRuntimeInputTests
    {
        [UnityTest]
        public IEnumerator ScriptedControlFrames_DriveMovementPrimaryActionPauseAndRespawnAcrossEveryGenre()
        {
            var modes = new[] { PrototypeMode.FirstPersonAction, PrototypeMode.Platformer, PrototypeMode.TopDownRpg, PrototypeMode.Racing };
            foreach (var mode in modes) yield return ExerciseMode(mode);
        }

        [UnityTest]
        public IEnumerator CampaignCompletion_PersistsMonotonicUnlockAndRequestsExactNextScene()
        {
            const string nextPath = "Assets/Scenes/Campaigns/RuntimeAcceptance-Level02.unity";
            var slot = PrototypeCampaignProgress.SlotFor("first_person_action", SceneManager.GetActiveScene().name);
            var root = new GameObject("Campaign progression runtime test");
            GameObject reloadRoot = null;
            PrototypeCampaignProgress.Clear(slot); PrototypeSceneNavigation.BeginCapture();
            try
            {
                var game = root.AddComponent<PrototypeGame>(); game.templateId = "first_person_action";
                game.levelNumber = 1; game.levelCount = 3; game.requiredObjectives = 1; game.nextScenePath = nextPath;
                yield return null;

                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(confirmPressed: true));
                yield return null; PrototypeInput.SetScriptedFrame(default); yield return null;
                Assert.That(game.Register(ObjectiveKind.Target, 0), Is.True, "Campaign objective did not complete through the runtime game contract.");
                Assert.That(game.Complete, Is.True); Assert.That(game.HighestUnlockedLevel, Is.EqualTo(2));
                Assert.That(PrototypeCampaignProgress.LoadHighestUnlocked(slot, 3), Is.EqualTo(2), "Completion did not persist the next-level unlock.");

                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(nextPressed: true));
                yield return null; PrototypeInput.SetScriptedFrame(default); yield return null;
                Assert.That(PrototypeSceneNavigation.CapturedPath, Is.EqualTo(nextPath), "Next input did not request the serialized campaign transition.");

                Object.DestroyImmediate(root); root = null;
                reloadRoot = new GameObject("Campaign reload runtime test");
                var reloaded = reloadRoot.AddComponent<PrototypeGame>(); reloaded.templateId = "first_person_action";
                reloaded.levelNumber = 2; reloaded.levelCount = 3; reloaded.requiredObjectives = 1;
                yield return null;
                Assert.That(reloaded.HighestUnlockedLevel, Is.EqualTo(2), "A new runtime game instance did not reload persisted progress.");
                Assert.That(PrototypeCampaignProgress.UnlockNext(slot, 2, 3), Is.EqualTo(3));
                Assert.That(PrototypeCampaignProgress.UnlockNext(slot, 1, 3), Is.EqualTo(3), "Replaying an earlier level regressed campaign progress.");
            }
            finally
            {
                PrototypeInput.ClearScriptedFrame(); PrototypeSceneNavigation.EndCapture(); PrototypeCampaignProgress.Clear(slot);
                Time.timeScale = 1f;
                if (reloadRoot != null) Object.DestroyImmediate(reloadRoot);
                if (root != null) Object.DestroyImmediate(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TopDownTarget_ChasesDealsContactDamageAndFallsToRealAttackFrames()
        {
            var root = new GameObject("Combat runtime test");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var actorObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var targetObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                ground.transform.position = new Vector3(0f, -0.5f, 0f); ground.transform.localScale = new Vector3(40f, 1f, 40f);
                actorObject.transform.position = new Vector3(0f, 1f, 0f); Object.DestroyImmediate(actorObject.GetComponent<Collider>());
                actorObject.AddComponent<CharacterController>(); var actor = actorObject.AddComponent<PrototypeActor>(); actor.mode = PrototypeMode.TopDownRpg;
                targetObject.transform.position = new Vector3(0f, 1f, 5f); var target = targetObject.AddComponent<PrototypeObjective>();
                target.kind = ObjectiveKind.Target; target.archetype = 1; target.health = 2;
                var game = root.AddComponent<PrototypeGame>(); game.templateId = "top_down_rpg"; game.requiredObjectives = 1;
                game.levelNumber = 1; game.levelCount = 1; game.player = actor;
                yield return null;

                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(confirmPressed: true));
                yield return null; PrototypeInput.SetScriptedFrame(default); yield return null;
                var initialDistance = Vector3.Distance(actor.transform.position, targetObject.transform.position);
                for (var frame = 0; frame < 20; frame++) yield return new WaitForSecondsRealtime(0.02f);
                Assert.That(Vector3.Distance(actor.transform.position, targetObject.transform.position), Is.LessThan(initialDistance - 0.2f), "Target did not chase through its runtime Update path.");

                targetObject.transform.position = actor.transform.position + Vector3.forward * 1.2f;
                var healthBeforeContact = game.Health;
                for (var frame = 0; frame < 5 && game.Health == healthBeforeContact; frame++) yield return new WaitForSecondsRealtime(0.02f);
                Assert.That(game.Health, Is.LessThan(healthBeforeContact), "Target did not deal cooldown-limited contact damage.");

                for (var attack = 0; attack < 4 && !game.Complete; attack++)
                {
                    PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(firePressed: true));
                    yield return null; PrototypeInput.SetScriptedFrame(default); yield return new WaitForSecondsRealtime(0.02f);
                }
                Assert.That(game.Complete, Is.True, "Real top-down attack frames did not complete the target objective.");
                Assert.That(targetObject.activeSelf, Is.False, "Defeated target remained active.");
            }
            finally
            {
                PrototypeInput.ClearScriptedFrame(); Time.timeScale = 1f;
                Object.DestroyImmediate(targetObject); Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(ground); Object.DestroyImmediate(root);
            }
            yield return null;
        }

        private static IEnumerator ExerciseMode(PrototypeMode mode)
        {
            var root = new GameObject("Runtime input test " + mode);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var actorObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var cameraObject = new GameObject("MainCamera");
            try
            {
                ground.name = "Ground"; ground.transform.position = new Vector3(0f, -0.5f, 0f);
                ground.transform.localScale = new Vector3(80f, 1f, 80f);
                actorObject.name = "Player"; actorObject.transform.position = new Vector3(0f, 1f, 0f);
                Object.DestroyImmediate(actorObject.GetComponent<Collider>());
                actorObject.AddComponent<CharacterController>();
                var actor = actorObject.AddComponent<PrototypeActor>(); actor.mode = mode;
                var camera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 7f, -10f); actor.view = camera.transform;
                var game = root.AddComponent<PrototypeGame>(); game.templateId = TemplateId(mode); game.requiredObjectives = 1;
                game.levelNumber = 1; game.levelCount = 1; game.player = actor;

                yield return null;
                Assert.That(game.MenuOpen, Is.True, mode + " did not present the first-level start state.");
                Assert.That(Time.timeScale, Is.EqualTo(0f), mode + " did not pause behind its start state.");

                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(confirmPressed: true));
                yield return null;
                PrototypeInput.SetScriptedFrame(default);
                yield return null;
                Assert.That(game.MenuOpen, Is.False, mode + " did not consume the scripted confirm frame.");
                Assert.That(Time.timeScale, Is.EqualTo(1f), mode + " did not resume gameplay after confirm.");

                for (var settle = 0; settle < 5; settle++) yield return null;
                var before = actor.transform.position; var startY = before.y; var maximumY = startY;
                var actionsBefore = actor.ActionEvents;
                for (var frame = 0; frame < 90; frame++)
                {
                    var moveX = mode == PrototypeMode.Platformer ? 1f : mode is PrototypeMode.TopDownRpg or PrototypeMode.Racing ? 0.45f : 0f;
                    var moveY = mode == PrototypeMode.Platformer ? 0f : 1f;
                    PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(moveX: moveX, moveY: moveY,
                        lookX: mode == PrototypeMode.FirstPersonAction ? 0.03f : 0f,
                        jumpPressed: mode == PrototypeMode.Platformer && actor.ActionEvents == actionsBefore,
                        firePressed: mode == PrototypeMode.TopDownRpg && frame == 10,
                        fireHeld: mode == PrototypeMode.FirstPersonAction));
                    yield return new WaitForSecondsRealtime(0.02f);
                    maximumY = Mathf.Max(maximumY, actor.transform.position.y);
                }
                PrototypeInput.SetScriptedFrame(default);
                yield return null;

                var movement = Vector3.Distance(before, actor.transform.position);
                Assert.That(movement, Is.GreaterThan(0.5f), mode + " did not move through its real Update/controller path.");
                Assert.That(actor.ActionEvents, Is.GreaterThan(actionsBefore), mode + " did not execute its primary action through the control frame.");
                if (mode == PrototypeMode.Platformer)
                    Assert.That(maximumY, Is.GreaterThan(startY + 0.2f), "Platformer jump did not produce a frame-observed vertical arc.");

                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(pausePressed: true));
                yield return null;
                PrototypeInput.SetScriptedFrame(default);
                yield return null;
                Assert.That(game.MenuOpen, Is.True, mode + " did not pause through its control frame.");
                Assert.That(Time.timeScale, Is.EqualTo(0f), mode + " pause did not stop scaled gameplay time.");
                var pausedPosition = actor.transform.position;
                for (var paused = 0; paused < 4; paused++)
                {
                    PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(moveX: 1f, moveY: 1f));
                    yield return null;
                }
                Assert.That(Vector3.Distance(pausedPosition, actor.transform.position), Is.LessThan(0.01f), mode + " moved while paused.");

                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(pausePressed: true));
                yield return null;
                PrototypeInput.SetScriptedFrame(default);
                yield return null;
                Assert.That(game.MenuOpen, Is.False, mode + " did not resume through its control frame.");
                actor.TeleportToSpawn();
                Assert.That(actor.IsAtSpawnForVerification(), Is.True, mode + " did not restore its runtime spawn.");
            }
            finally
            {
                PrototypeInput.ClearScriptedFrame(); Time.timeScale = 1f;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(ground); Object.DestroyImmediate(root);
            }
            yield return null;
        }

        private static string TemplateId(PrototypeMode mode)
        {
            switch (mode)
            {
                case PrototypeMode.FirstPersonAction: return "first_person_action";
                case PrototypeMode.Platformer: return "platformer";
                case PrototypeMode.TopDownRpg: return "top_down_rpg";
                case PrototypeMode.Racing: return "racing";
                default: throw new System.ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }
}
""";

    private const string PrototypeTypesSource = """
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    public enum PrototypeMode { FirstPersonAction, Platformer, TopDownRpg, Racing }
    public enum ObjectiveKind { Target, Collectible, Checkpoint, Finish, Hazard }
    public enum SurfaceKind { Ground, Structure, Objective, Weapon, Actor }

    public readonly struct PrototypeInputFrame
    {
        public readonly float MoveX; public readonly float MoveY; public readonly float LookX; public readonly float LookY;
        public readonly bool ConfirmPressed; public readonly bool PausePressed; public readonly bool RestartPressed;
        public readonly bool NextPressed; public readonly bool QuitPressed; public readonly bool SelectLeftPressed;
        public readonly bool SelectRightPressed; public readonly bool JumpPressed; public readonly bool FirePressed;
        public readonly bool FireHeld; public readonly bool ReloadPressed;

        public PrototypeInputFrame(float moveX = 0f, float moveY = 0f, float lookX = 0f, float lookY = 0f,
            bool confirmPressed = false, bool pausePressed = false, bool restartPressed = false,
            bool nextPressed = false, bool quitPressed = false, bool selectLeftPressed = false,
            bool selectRightPressed = false, bool jumpPressed = false, bool firePressed = false,
            bool fireHeld = false, bool reloadPressed = false)
        {
            MoveX = Mathf.Clamp(moveX, -1f, 1f); MoveY = Mathf.Clamp(moveY, -1f, 1f);
            LookX = Mathf.Clamp(lookX, -1f, 1f); LookY = Mathf.Clamp(lookY, -1f, 1f);
            ConfirmPressed = confirmPressed; PausePressed = pausePressed; RestartPressed = restartPressed;
            NextPressed = nextPressed; QuitPressed = quitPressed; SelectLeftPressed = selectLeftPressed;
            SelectRightPressed = selectRightPressed; JumpPressed = jumpPressed; FirePressed = firePressed;
            FireHeld = fireHeld; ReloadPressed = reloadPressed;
        }
    }

    public static class PrototypeInput
    {
        private static bool scripted;
        private static PrototypeInputFrame scriptedFrame;

        public static PrototypeInputFrame Read()
        {
            if (scripted) return scriptedFrame;
            var moveX = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            var moveY = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            var firePressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
            return new PrototypeInputFrame(moveX, moveY, Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"),
                Input.GetKeyDown(KeyCode.Return), Input.GetKeyDown(KeyCode.Escape), Input.GetKeyDown(KeyCode.F5),
                Input.GetKeyDown(KeyCode.N), Input.GetKeyDown(KeyCode.Q), Input.GetKeyDown(KeyCode.LeftArrow),
                Input.GetKeyDown(KeyCode.RightArrow), Input.GetKeyDown(KeyCode.Space), firePressed,
                Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space), Input.GetKeyDown(KeyCode.R));
        }

        public static void SetScriptedFrame(PrototypeInputFrame frame) { scripted = true; scriptedFrame = frame; }
        public static void ClearScriptedFrame() { scriptedFrame = default; scripted = false; }
    }
}
""";

    private const string PrototypeCampaignProgressSource = """
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeCoreGameTemplates
{
    public static class PrototypeCampaignProgress
    {
        public static string SlotFor(string templateId, string sceneName)
        {
            var template = string.IsNullOrWhiteSpace(templateId) ? "prototype" : templateId.Trim().ToLowerInvariant();
            var campaign = string.IsNullOrWhiteSpace(sceneName) ? "scene" : sceneName.Trim();
            var marker = campaign.LastIndexOf("-Level", StringComparison.Ordinal);
            if (marker >= 0 && campaign.Length == marker + 8 && char.IsDigit(campaign[marker + 6]) && char.IsDigit(campaign[marker + 7]))
                campaign = campaign.Substring(0, marker);
            var identity = template + "|" + campaign.ToLowerInvariant();
            uint hash = 2166136261;
            foreach (var character in identity)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return "ForgeCore.Progress.v1." + hash.ToString("x8");
        }

        public static int LoadHighestUnlocked(string slot, int levelCount)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(slot, 1), 1, Mathf.Max(1, levelCount));
        }

        public static string ScenePathForLevel(string currentScenePath, int level, int levelCount)
        {
            if (level < 1 || level > Mathf.Max(1, levelCount)) throw new ArgumentOutOfRangeException(nameof(level));
            var path = (currentScenePath ?? string.Empty).Replace('\\', '/');
            var marker = path.LastIndexOf("-Level", StringComparison.Ordinal);
            if (marker < 7 || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || path.Length != marker + 14)
            {
                if (level == 1 && !string.IsNullOrWhiteSpace(path)) return path;
                throw new InvalidOperationException("Level selection requires a ForgeCore -LevelNN.unity campaign scene.");
            }
            return path.Substring(0, marker + 6) + level.ToString("00") + ".unity";
        }

        public static int UnlockNext(string slot, int completedLevel, int levelCount)
        {
            var unlocked = Mathf.Clamp(completedLevel + 1, 1, Mathf.Max(1, levelCount));
            var current = LoadHighestUnlocked(slot, levelCount);
            if (unlocked > current)
            {
                PlayerPrefs.SetInt(slot, unlocked);
                PlayerPrefs.Save();
                return unlocked;
            }
            return current;
        }

        public static void Clear(string slot)
        {
            PlayerPrefs.DeleteKey(slot);
            PlayerPrefs.Save();
        }
    }

    public static class PrototypeSceneNavigation
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static string CapturedPath { get; private set; } = "";
        private static bool capture;
        public static void BeginCapture() { CapturedPath = ""; capture = true; }
        public static void EndCapture() { CapturedPath = ""; capture = false; }
#endif
        public static void LoadPath(string path)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (capture) { CapturedPath = path ?? ""; return; }
#endif
            SceneManager.LoadScene(path);
        }

        public static void LoadBuildIndex(int buildIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (capture) { CapturedPath = "build:" + buildIndex; return; }
#endif
            SceneManager.LoadScene(buildIndex);
        }
    }
}
""";

    private const string PrototypeFeedbackSource = """
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    public static class PrototypeFeedback
    {
        private const int SampleRate = 22050;
        private static AudioClip hit;
        private static AudioClip objective;
        private static AudioClip damage;
        private static AudioClip fire;

        public static void Fire(Vector3 position)
        {
            Play(position, ref fire, 190f, 0.055f, 0.13f);
            Burst(position, new Color(1f, 0.72f, 0.2f), 5);
        }

        public static void Hit(Vector3 position, bool defeated)
        {
            if (defeated) Play(position, ref objective, 720f, 0.15f, 0.18f);
            else Play(position, ref hit, 310f, 0.07f, 0.1f);
            Burst(position, defeated ? new Color(0.2f, 1f, 0.7f) : new Color(1f, 0.35f, 0.12f), defeated ? 16 : 7);
        }

        public static void Objective(Vector3 position, ObjectiveKind kind)
        {
            Play(position, ref objective, kind == ObjectiveKind.Checkpoint ? 620f : 820f, 0.14f, 0.16f);
            Burst(position, new Color(0.2f, 0.8f, 1f), 18);
        }

        public static void Damage(Vector3 position)
        {
            Play(position, ref damage, 145f, 0.11f, 0.14f);
        }

        private static void Play(Vector3 position, ref AudioClip clip, float frequency, float duration, float volume)
        {
            if (!Application.isPlaying) return;
            if (clip == null) clip = CreateTone("ForgeCore feedback", frequency, duration);
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        private static AudioClip CreateTone(string name, float frequency, float duration)
        {
            var samples = Mathf.Clamp(Mathf.RoundToInt(SampleRate * duration), 256, SampleRate);
            var data = new float[samples];
            for (var index = 0; index < samples; index++)
            {
                var envelope = 1f - index / (float)samples;
                data[index] = Mathf.Sin(2f * Mathf.PI * frequency * index / SampleRate) * envelope * 0.6f;
            }
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false); clip.SetData(data, 0); return clip;
        }

        private static void Burst(Vector3 position, Color color, int count)
        {
            if (!Application.isPlaying) return;
            var value = new GameObject("ForgeCore feedback burst"); value.transform.position = position;
            var particles = value.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.duration = 0.18f; main.loop = false; main.startLifetime = 0.38f;
            main.startSpeed = 3.2f; main.startSize = 0.14f; main.startColor = color;
            main.maxParticles = 24; main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.22f;
            particles.Play();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static AudioClip CreateToneForVerification() => CreateTone("ForgeCore verification tone", 440f, 0.1f);
#endif
    }
}
""";

    private const string PrototypeSurfaceLibrarySource = """
using System.Collections.Generic;
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    public static class PrototypeSurfaceLibrary
    {
        private const int Size = 64;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static void Apply(Material material, Color color, SurfaceKind kind, int seed)
        {
            var texture = Get(kind, seed); material.color = color; material.mainTexture = texture;
            var tiling = kind == SurfaceKind.Ground ? new Vector2(8f, 8f) : kind == SurfaceKind.Structure ? new Vector2(3f, 3f) : Vector2.one;
            material.mainTextureScale = tiling;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_BaseMap")) { material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", tiling); }
            if (material.HasProperty("_MainTex")) { material.SetTexture("_MainTex", texture); material.SetTextureScale("_MainTex", tiling); }
            var smoothness = kind == SurfaceKind.Weapon ? 0.68f : kind == SurfaceKind.Objective ? 0.5f : kind == SurfaceKind.Ground ? 0.16f : 0.28f;
            var metallic = kind == SurfaceKind.Weapon ? 0.72f : kind == SurfaceKind.Objective ? 0.18f : 0.04f;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (kind == SurfaceKind.Objective && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.25f);
            }
        }

        private static Texture2D Get(SurfaceKind kind, int seed)
        {
            var key = kind + ":" + seed;
            if (!Cache.TryGetValue(key, out var texture)) { texture = Build(kind, seed); Cache.Add(key, texture); }
            return texture;
        }

        private static Texture2D Build(SurfaceKind kind, int seed)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = "ForgeCore " + kind + " surface " + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var noise = Noise(x, y, seed); var fine = Noise(x * 3 + 7, y * 3 + 11, seed + 97); float value;
                if (kind == SurfaceKind.Ground)
                {
                    value = 0.52f + noise * 0.34f + fine * 0.08f;
                    if ((x + seed * 3) % 31 == 0 || (y + seed * 5) % 37 == 0) value *= 0.48f;
                }
                else if (kind == SurfaceKind.Structure)
                {
                    value = 0.58f + noise * 0.28f + fine * 0.08f;
                    if ((x + seed) % 16 <= 1 || (y + seed * 2) % 12 <= 1) value *= 0.58f;
                    if (fine > 0.965f) value *= 0.55f;
                }
                else if (kind == SurfaceKind.Weapon)
                {
                    value = 0.58f + noise * 0.18f + (y % 4 == 0 ? 0.14f : 0f);
                    if ((x + seed) % 23 == 0) value *= 0.62f;
                }
                else if (kind == SurfaceKind.Objective)
                {
                    value = 0.68f + 0.18f * Mathf.Sin((x + seed) * 0.33f) + noise * 0.14f;
                    if (fine > 0.91f) value *= 0.7f;
                }
                else
                {
                    value = 0.62f + noise * 0.25f + 0.08f * Mathf.Sin((x + y + seed) * 0.2f);
                }
                var channel = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 24, 255);
                pixels[y * Size + x] = new Color32(channel, channel, channel, 255);
            }
            texture.SetPixels32(pixels); texture.Apply(true, false); return texture;
        }

        private static float Noise(int x, int y, int seed)
        {
            unchecked
            {
                var value = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
                value = (value ^ (value >> 13)) * 1274126177u; value ^= value >> 16;
                return (value & 0xffffu) / 65535f;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static Texture2D CreateForVerification(SurfaceKind kind, int seed) => Build(kind, seed);
#endif
    }
}
""";

    private const string PrototypeGameSource = """
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace ForgeCoreGameTemplates
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PrototypeGame : MonoBehaviour
    {
        private const string PlayerProfileTemplateKitVersion = "0.16.33";
        private const string PlayerProfileScenario = "scripted_gameplay_v1";
        [Serializable]
        private sealed class PlayerProfileRequest
        {
            public int schemaVersion; public string requestId; public string templateKitVersion; public string outputPath;
            public string executableSha256; public string level0Sha256; public string buildProvenanceSha256;
            public string scenePath; public string sceneSha256; public string profileScenario;
            public int durationSeconds; public int width; public int height;
            public string createdUtc;
        }
        [Serializable]
        private sealed class PlayerProfileResult
        {
            public int schemaVersion; public string requestId; public string requestSha256; public string templateKitVersion;
            public string environment; public string executableSha256; public string level0Sha256; public string buildProvenanceSha256;
            public string scenePath; public string sceneSha256; public int durationSeconds; public int width; public int height;
            public int sceneBuildIndex; public string sceneName; public int sampledFrames; public double averageFrameMilliseconds;
            public double p95FrameMilliseconds; public double maximumFrameMilliseconds; public bool gpuTimingAvailable;
            public int sampledGpuFrames; public double averageGpuMilliseconds; public double p95GpuMilliseconds;
            public double maximumGpuMilliseconds; public long peakAllocatedMemoryBytes; public int rendererCount;
            public int meshCount; public long vertexCount; public long triangleCount; public int cameraCount;
            public string graphicsDeviceName; public string graphicsDeviceType; public string graphicsDeviceVersion;
            public int qualityLevel; public int vSyncCount; public int targetFrameRate; public string profileScenario;
            public string templateId; public int gameplayFrames; public double playerTravelMetres; public int actionEvents;
            public int completedObjectives; public int requiredObjectives; public int minimumHealth; public string completedUtc;
        }
        public static PrototypeGame Instance { get; private set; }
        public string templateId = "prototype";
        public int requiredObjectives = 1;
        public PrototypeActor player;
        public int levelNumber = 1;
        public int levelCount = 1;
        public string nextScenePath = "";
        public int CompletedObjectives { get; private set; }
        public int NextCheckpoint { get; private set; }
        public int Health { get; private set; } = 100;
        public bool Complete { get; private set; }
        public bool MenuOpen { get; private set; }
        public int HighestUnlockedLevel { get; private set; } = 1;
        private string message = "Complete the objective";
        private float messageUntil;
        private bool started;
        private string progressSlot;
        private int selectedLevel = 1;
        private bool playerProfileActive;
        private PlayerProfileRequest playerProfileRequest;
        private string playerProfileResultPath;
        private string playerProfileRequestSha256;
        private double playerProfileWarmupEndsAt;
        private double playerProfileEndsAt;
        private long playerProfilePeakMemory;
        private readonly List<double> playerProfileFrameMilliseconds = new List<double>(4096);
        private readonly List<double> playerProfileGpuMilliseconds = new List<double>(4096);
        private readonly FrameTiming[] playerProfileFrameTimings = new FrameTiming[1];
        private Vector3 playerProfileLastPosition;
        private double playerProfileTravelMetres;
        private int playerProfileGameplayFrames;
        private int playerProfileMinimumHealth = 100;
        private int playerProfileLastActionPulse = -1;

        private void Awake() { Instance = this; }
        private void Start()
        {
            ApplyVisualTheme();
            progressSlot = PrototypeCampaignProgress.SlotFor(templateId, SceneManager.GetActiveScene().name);
            HighestUnlockedLevel = PrototypeCampaignProgress.LoadHighestUnlocked(progressSlot, levelCount);
            selectedLevel = Mathf.Clamp(levelNumber, 1, HighestUnlockedLevel);
            var profileRequested = TryBeginPlayerProfile();
            if (profileRequested && !playerProfileActive) return;
            if (playerProfileActive) { started = true; SetMenuOpen(false); }
            else { started = levelNumber > 1; SetMenuOpen(!started); }
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (playerProfileActive) PrototypeInput.ClearScriptedFrame();
            Time.timeScale = 1f;
        }
        private void ApplyVisualTheme()
        {
            var art = GetComponent<PrototypeArtDirection>();
            if (templateId == "first_person_action" && art != null) { art.Apply(); return; }
            Color ground; Color structure; Color objective; Color accent; Color sky; Color fog;
            if (templateId == "platformer")
            {
                ground = new Color(0.08f, 0.17f, 0.25f); structure = new Color(0.12f, 0.42f, 0.55f);
                objective = new Color(0.35f, 1f, 0.9f); accent = new Color(1f, 0.42f, 0.18f); sky = new Color(0.05f, 0.12f, 0.22f); fog = new Color(0.08f, 0.18f, 0.28f);
            }
            else if (templateId == "top_down_rpg")
            {
                ground = new Color(0.07f, 0.13f, 0.09f); structure = new Color(0.18f, 0.28f, 0.16f);
                objective = new Color(0.95f, 0.24f, 0.18f); accent = new Color(0.96f, 0.72f, 0.2f); sky = new Color(0.08f, 0.13f, 0.1f); fog = new Color(0.11f, 0.2f, 0.14f);
            }
            else if (templateId == "racing")
            {
                ground = new Color(0.035f, 0.045f, 0.06f); structure = new Color(0.15f, 0.18f, 0.24f);
                objective = new Color(1f, 0.48f, 0.06f); accent = new Color(0.05f, 0.72f, 1f); sky = new Color(0.025f, 0.04f, 0.09f); fog = new Color(0.06f, 0.08f, 0.15f);
            }
            else
            {
                ground = new Color(0.055f, 0.075f, 0.11f); structure = new Color(0.13f, 0.22f, 0.32f);
                objective = new Color(1f, 0.16f, 0.075f); accent = new Color(0.05f, 0.65f, 1f); sky = new Color(0.035f, 0.07f, 0.13f); fog = new Color(0.07f, 0.12f, 0.2f);
            }
            var levelTint = Color.Lerp(Color.white, new Color(0.72f, 0.85f, 1f), Mathf.Clamp01((levelNumber - 1) * 0.16f));
            structure *= levelTint; objective *= levelTint;
            RenderSettings.ambientLight = structure * 0.72f; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog; RenderSettings.fogStartDistance = 20f; RenderSettings.fogEndDistance = 65f;
            var camera = Camera.main;
            if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = sky; camera.allowHDR = true; }
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = new Color(1f, 0.89f, 0.76f); light.intensity = 1.45f; light.shadows = LightShadows.Soft;
            }
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var item = renderer.GetComponentInParent<PrototypeObjective>(); var name = renderer.gameObject.name;
                var surface = item != null ? SurfaceKind.Objective : name.Contains("Weapon") ? SurfaceKind.Weapon :
                    player != null && renderer.gameObject == player.gameObject ? SurfaceKind.Actor : name.StartsWith("Ground") ? SurfaceKind.Ground : SurfaceKind.Structure;
                var color = item != null ? objective : name.Contains("Weapon") ? accent * 0.72f : player != null && renderer.gameObject == player.gameObject ? accent :
                    name.StartsWith("Ground") ? ground : name.Contains("Wall") || name.Contains("Platform") ? structure : structure * 0.82f;
                ApplyMaterial(renderer, color, surface, levelNumber);
            }
        }
        private static void ApplyMaterial(Renderer renderer, Color color, SurfaceKind surface, int seed)
        {
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { renderer.material.color = color; return; }
            var material = new Material(shader); PrototypeSurfaceLibrary.Apply(material, color, surface, seed);
            renderer.material = material;
        }
        public bool Register(ObjectiveKind kind, int order)
        {
            if (Complete) return false;
            if (kind == ObjectiveKind.Checkpoint && order != NextCheckpoint) return false;
            if (kind == ObjectiveKind.Checkpoint) NextCheckpoint++;
            CompletedObjectives++;
            message = kind == ObjectiveKind.Checkpoint ? "Checkpoint " + CompletedObjectives + " cleared" : "Objective " + CompletedObjectives + " complete";
            messageUntil = Time.time + 2f;
            if (CompletedObjectives >= requiredObjectives)
            {
                Complete = true; message = "LEVEL COMPLETE - press N to continue";
                if (Application.isPlaying)
                {
                    if (string.IsNullOrEmpty(progressSlot)) progressSlot = PrototypeCampaignProgress.SlotFor(templateId, SceneManager.GetActiveScene().name);
                    HighestUnlockedLevel = PrototypeCampaignProgress.UnlockNext(progressSlot, levelNumber, levelCount);
                }
            }
            return true;
        }
        public void Damage(int amount)
        {
            if (Complete) return;
            var applied = Mathf.Max(0, amount);
            if (applied == 0) return;
            Health = Mathf.Max(0, Health - applied); message = "Damage taken"; messageUntil = Time.time + 1.5f;
            PrototypeFeedback.Damage(player != null ? player.transform.position : transform.position);
            if (Health == 0) Respawn();
        }
        public void Respawn()
        {
            Health = 100; message = "Recovered at start"; messageUntil = Time.time + 2f;
            if (player != null) { player.TeleportToSpawn(); player.RestoreLoadout(); }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string RunAutomatedGameplayCheck()
        {
            BeginGameplay();
            if (player == null) throw new InvalidOperationException("Runtime gameplay check requires the serialized player.");
            if (Camera.main == null) throw new InvalidOperationException("Runtime gameplay check requires the active MainCamera.");
            var movement = player.AutomatedMoveForVerification();
            if (movement < 0.5f) throw new InvalidOperationException("Player did not move far enough during the deterministic runtime check.");
            player.TeleportToSpawn();
            if (!player.IsAtSpawnForVerification()) throw new InvalidOperationException("Player did not return to its runtime spawn.");
            var objectives = UnityEngine.Object.FindObjectsByType<PrototypeObjective>(FindObjectsSortMode.None);
            Array.Sort(objectives, (left, right) => left.order.CompareTo(right.order));
            if (objectives.Length != requiredObjectives) throw new InvalidOperationException("Runtime objective count differs from the serialized gameplay contract.");
            if (templateId == "racing" && objectives.Length > 1)
            {
                if (objectives[1].AutomatedInteractForVerification() || CompletedObjectives != 0)
                    throw new InvalidOperationException("Runtime racing loop accepted an out-of-order checkpoint.");
            }
            foreach (var objective in objectives)
                if (!objective.AutomatedInteractForVerification()) throw new InvalidOperationException("Runtime objective interaction did not register in order.");
            if (!Complete || CompletedObjectives != requiredObjectives)
                throw new InvalidOperationException("Runtime objective loop did not reach completion.");
            return "Deterministic runtime gameplay check passed for " + templateId + ": movement=" + movement.ToString("0.00", CultureInfo.InvariantCulture) + "m, respawn=passed, objectives=" + CompletedObjectives + "/" + requiredObjectives + ".";
        }
#endif
        private void Update()
        {
            if (UpdatePlayerProfile()) return;
            var input = PrototypeInput.Read();
            if (!started && input.ConfirmPressed) ActivateSelectedLevelOrResume();
            else if (started && input.PausePressed) SetMenuOpen(!MenuOpen);
            if (MenuOpen)
            {
                if (input.SelectLeftPressed) selectedLevel = Mathf.Max(1, selectedLevel - 1);
                if (input.SelectRightPressed) selectedLevel = Mathf.Min(HighestUnlockedLevel, selectedLevel + 1);
                if (input.ReloadPressed) RestartLevel();
                if (input.QuitPressed) QuitGame();
                return;
            }
            if (input.RestartPressed) PrototypeSceneNavigation.LoadBuildIndex(SceneManager.GetActiveScene().buildIndex);
            if (Complete && !string.IsNullOrEmpty(nextScenePath) && input.NextPressed) PrototypeSceneNavigation.LoadPath(nextScenePath);
        }
        private bool TryBeginPlayerProfile()
        {
            var arguments = Environment.GetCommandLineArgs();
            var hasRequest = HasArgument(arguments, "-forgecoreProfileRequest");
            var hasResult = HasArgument(arguments, "-forgecoreProfileResult");
            var hasHash = HasArgument(arguments, "-forgecoreProfileRequestSha256");
            if (!hasRequest && !hasResult && !hasHash) return false;
            try
            {
                if (!hasRequest || !hasResult || !hasHash) throw new InvalidDataException("ForgeCore player profile requires all three profile arguments.");
                var requestPath = GetSingleArgumentValue(arguments, "-forgecoreProfileRequest");
                playerProfileResultPath = GetSingleArgumentValue(arguments, "-forgecoreProfileResult");
                playerProfileRequestSha256 = GetSingleArgumentValue(arguments, "-forgecoreProfileRequestSha256");
                if (!Path.IsPathRooted(requestPath) || !Path.IsPathRooted(playerProfileResultPath) ||
                    !string.Equals(Path.GetExtension(requestPath), ".json", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetExtension(playerProfileResultPath), ".json", StringComparison.OrdinalIgnoreCase) ||
                    playerProfileRequestSha256.Length != 64 || !IsLowerHex(playerProfileRequestSha256))
                    throw new InvalidDataException("ForgeCore player profile paths or request hash are invalid.");
                requestPath = Path.GetFullPath(requestPath); playerProfileResultPath = Path.GetFullPath(playerProfileResultPath);
                if (!File.Exists(requestPath) || File.Exists(playerProfileResultPath) || Directory.Exists(playerProfileResultPath))
                    throw new InvalidDataException("ForgeCore player profile request is missing or its non-overwriting result destination already exists.");
                var bytes = File.ReadAllBytes(requestPath);
                if (bytes.Length < 2 || bytes.Length > 1048576 || !FixedTimeEqualsHex(Hash(bytes), playerProfileRequestSha256))
                    throw new InvalidDataException("ForgeCore player profile request bytes do not match the approved hash.");
                playerProfileRequest = JsonUtility.FromJson<PlayerProfileRequest>(Encoding.UTF8.GetString(bytes));
                if (playerProfileRequest == null || playerProfileRequest.schemaVersion != 2 ||
                    string.IsNullOrEmpty(playerProfileRequest.requestId) || playerProfileRequest.requestId.Length != 32 || !IsLowerHex(playerProfileRequest.requestId) ||
                    !string.Equals(playerProfileRequest.templateKitVersion, PlayerProfileTemplateKitVersion, StringComparison.Ordinal) ||
                    !IsLowerSha256(playerProfileRequest.executableSha256) || !IsLowerSha256(playerProfileRequest.level0Sha256) ||
                    !IsLowerSha256(playerProfileRequest.buildProvenanceSha256) || !IsLowerSha256(playerProfileRequest.sceneSha256) ||
                    string.IsNullOrEmpty(playerProfileRequest.scenePath) || !playerProfileRequest.scenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !playerProfileRequest.scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || playerProfileRequest.scenePath.Contains("..") ||
                    !string.Equals(playerProfileRequest.profileScenario, PlayerProfileScenario, StringComparison.Ordinal) ||
                    playerProfileRequest.durationSeconds < 5 || playerProfileRequest.durationSeconds > 30 ||
                    playerProfileRequest.width != 1280 || playerProfileRequest.height != 720 ||
                    !DateTime.TryParse(playerProfileRequest.createdUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    throw new InvalidDataException("ForgeCore player profile request identity is outside the reviewed runtime contract.");
                if (player == null) throw new InvalidDataException("ForgeCore scripted gameplay profile requires the serialized player.");
                Directory.CreateDirectory(Path.GetDirectoryName(playerProfileResultPath));
                Screen.SetResolution(playerProfileRequest.width, playerProfileRequest.height, FullScreenMode.Windowed);
                playerProfilePeakMemory = Profiler.GetTotalAllocatedMemoryLong();
                playerProfileLastPosition = player.transform.position;
                playerProfileMinimumHealth = Health;
                playerProfileWarmupEndsAt = Time.realtimeSinceStartupAsDouble + 1d;
                playerProfileEndsAt = playerProfileWarmupEndsAt + playerProfileRequest.durationSeconds;
                playerProfileActive = true;
                Debug.Log("ForgeCore standalone player profile started for " + playerProfileRequest.durationSeconds + " seconds at " + playerProfileRequest.width + "x" + playerProfileRequest.height + ".");
            }
            catch (Exception exception)
            {
                Debug.LogError("ForgeCore player profile initialization failed: " + exception);
                Application.Quit(3);
            }
            return true;
        }
        private bool UpdatePlayerProfile()
        {
            if (!playerProfileActive) return false;
            FrameTimingManager.CaptureFrameTimings();
            var now = Time.realtimeSinceStartupAsDouble;
            var currentPosition = player.transform.position;
            if (now >= playerProfileWarmupEndsAt)
            {
                var travelled = Vector3.Distance(playerProfileLastPosition, currentPosition);
                if (travelled >= 0f && travelled <= 100f) playerProfileTravelMetres += travelled;
                playerProfileGameplayFrames++;
                playerProfileMinimumHealth = Math.Min(playerProfileMinimumHealth, Health);
            }
            playerProfileLastPosition = currentPosition;
            PrototypeInput.SetScriptedFrame(CreatePlayerProfileInput(Math.Max(0d, now - playerProfileWarmupEndsAt)));
            if (now < playerProfileWarmupEndsAt) return true;
            var frameMilliseconds = Time.unscaledDeltaTime * 1000d;
            if (frameMilliseconds > 0d && !double.IsNaN(frameMilliseconds) && !double.IsInfinity(frameMilliseconds))
                playerProfileFrameMilliseconds.Add(frameMilliseconds);
            playerProfilePeakMemory = Math.Max(playerProfilePeakMemory, Profiler.GetTotalAllocatedMemoryLong());
            if (FrameTimingManager.GetLatestTimings(1, playerProfileFrameTimings) > 0)
            {
                var gpu = playerProfileFrameTimings[0].gpuFrameTime;
                if (gpu > 0d && !double.IsNaN(gpu) && !double.IsInfinity(gpu)) playerProfileGpuMilliseconds.Add(gpu);
            }
            if (now < playerProfileEndsAt) return false;
            try { CompletePlayerProfile(); }
            catch (Exception exception)
            {
                Debug.LogError("ForgeCore player profile completion failed: " + exception);
                Application.Quit(4);
            }
            return true;
        }
        private PrototypeInputFrame CreatePlayerProfileInput(double elapsedSeconds)
        {
            var cycle = elapsedSeconds % 4d;
            var pulse = (int)Math.Floor(elapsedSeconds / 0.45d);
            var actionPressed = pulse != playerProfileLastActionPulse;
            if (actionPressed) playerProfileLastActionPulse = pulse;
            if (templateId == "platformer")
                return new PrototypeInputFrame(moveX: cycle < 2d ? 1f : -1f, jumpPressed: actionPressed);
            if (templateId == "top_down_rpg")
                return new PrototypeInputFrame(moveX: cycle < 2d ? 0.8f : -0.8f, moveY: cycle < 1d || cycle >= 3d ? 0.65f : -0.65f,
                    firePressed: actionPressed);
            if (templateId == "racing")
                return new PrototypeInputFrame(moveX: cycle < 2d ? 0.55f : -0.55f, moveY: 1f);
            return new PrototypeInputFrame(moveX: cycle < 2d ? 0.35f : -0.35f, moveY: cycle < 3d ? 1f : -0.35f,
                fireHeld: true, reloadPressed: actionPressed && elapsedSeconds > 1d);
        }
        private void CompletePlayerProfile()
        {
            if (player != null && player.weaponView != null)
            {
                var pose = player.weaponView.GetComponentInChildren<PrototypeCombatAnimation>();
                if (pose != null)
                {
                    var enemyShots = 0; var enemyReloads = 0;
                    foreach (var enemy in UnityEngine.Object.FindObjectsByType<PrototypeRangedEnemy>(FindObjectsSortMode.None))
                    { enemyShots += enemy.ShotsFired; enemyReloads += enemy.ReloadCount; }
                    if (PrototypeProjectile.PlayerLaunchCount == 0 || enemyShots == 0 || pose.ReloadAnimations == 0)
                        throw new InvalidOperationException("Animated combat profile did not exercise both shooters and player reload.");
                    Debug.Log("ForgeCore animated combat exercised: player_bullets=" + PrototypeProjectile.PlayerLaunchCount +
                        ", enemy_shots=" + enemyShots + ", player_reload_animations=" + pose.ReloadAnimations + ", enemy_reloads=" + enemyReloads + ".");
                }
            }
            if (playerProfileFrameMilliseconds.Count < 30) throw new InvalidDataException("ForgeCore player profile did not collect enough rendered frames.");
            if (playerProfileGameplayFrames < 30 || playerProfileTravelMetres < 0.25d || player.ActionEvents < 1)
                throw new InvalidDataException("ForgeCore scripted gameplay profile did not exercise movement and genre action paths.");
            var result = new PlayerProfileResult
            {
                schemaVersion = 2, requestId = playerProfileRequest.requestId, requestSha256 = playerProfileRequestSha256,
                templateKitVersion = PlayerProfileTemplateKitVersion, environment = "standalone_player",
                executableSha256 = playerProfileRequest.executableSha256, level0Sha256 = playerProfileRequest.level0Sha256,
                buildProvenanceSha256 = playerProfileRequest.buildProvenanceSha256, scenePath = playerProfileRequest.scenePath,
                sceneSha256 = playerProfileRequest.sceneSha256, durationSeconds = playerProfileRequest.durationSeconds,
                width = Screen.width, height = Screen.height, sceneBuildIndex = SceneManager.GetActiveScene().buildIndex,
                sceneName = SceneManager.GetActiveScene().name, sampledFrames = playerProfileFrameMilliseconds.Count,
                averageFrameMilliseconds = Average(playerProfileFrameMilliseconds),
                p95FrameMilliseconds = Percentile95(playerProfileFrameMilliseconds),
                maximumFrameMilliseconds = Maximum(playerProfileFrameMilliseconds),
                gpuTimingAvailable = playerProfileGpuMilliseconds.Count > 0, sampledGpuFrames = playerProfileGpuMilliseconds.Count,
                averageGpuMilliseconds = Average(playerProfileGpuMilliseconds), p95GpuMilliseconds = Percentile95(playerProfileGpuMilliseconds),
                maximumGpuMilliseconds = Maximum(playerProfileGpuMilliseconds), peakAllocatedMemoryBytes = playerProfilePeakMemory,
                graphicsDeviceName = SystemInfo.graphicsDeviceName, graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion, qualityLevel = QualitySettings.GetQualityLevel(),
                vSyncCount = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate,
                profileScenario = PlayerProfileScenario, templateId = templateId, gameplayFrames = playerProfileGameplayFrames,
                playerTravelMetres = playerProfileTravelMetres, actionEvents = player.ActionEvents,
                completedObjectives = CompletedObjectives, requiredObjectives = requiredObjectives, minimumHealth = playerProfileMinimumHealth,
                completedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            PopulateSceneComplexity(result);
            var temporary = playerProfileResultPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
                File.Move(temporary, playerProfileResultPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            PrototypeInput.ClearScriptedFrame(); playerProfileActive = false;
            Debug.Log("ForgeCore standalone player profile completed: frames=" + result.sampledFrames +
                ", p95=" + result.p95FrameMilliseconds.ToString("0.00", CultureInfo.InvariantCulture) + "ms.");
            Application.Quit(0);
        }
        private static void PopulateSceneComplexity(PlayerProfileResult result)
        {
            result.rendererCount = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            result.cameraCount = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            var meshes = new HashSet<Mesh>();
            foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                if (renderer.sharedMesh != null) meshes.Add(renderer.sharedMesh);
            result.meshCount = meshes.Count;
            foreach (var mesh in meshes)
            {
                result.vertexCount += mesh.vertexCount;
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    result.triangleCount += (long)mesh.GetIndexCount(subMesh) / 3L;
            }
        }
        private static double Average(List<double> values)
        {
            if (values.Count == 0) return 0d;
            double total = 0d; foreach (var value in values) total += value; return total / values.Count;
        }
        private static double Percentile95(List<double> values)
        {
            if (values.Count == 0) return 0d;
            var copy = values.ToArray(); Array.Sort(copy); return copy[Mathf.Clamp(Mathf.CeilToInt(copy.Length * 0.95f) - 1, 0, copy.Length - 1)];
        }
        private static double Maximum(List<double> values)
        {
            if (values.Count == 0) return 0d;
            var maximum = values[0]; for (var index = 1; index < values.Count; index++) maximum = Math.Max(maximum, values[index]); return maximum;
        }
        private static bool HasArgument(string[] arguments, string name) => Array.IndexOf(arguments, name) >= 0;
        private static string GetSingleArgumentValue(string[] arguments, string name)
        {
            string value = null;
            for (var index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], name, StringComparison.Ordinal)) continue;
                if (value != null || index + 1 >= arguments.Length || string.IsNullOrEmpty(arguments[index + 1]))
                    throw new InvalidDataException("ForgeCore player profile argument is missing or duplicated: " + name);
                value = arguments[++index];
            }
            return value ?? throw new InvalidDataException("ForgeCore player profile argument is missing: " + name);
        }
        private static bool IsLowerSha256(string value) => value != null && value.Length == 64 && IsLowerHex(value);
        private static bool IsLowerHex(string value)
        {
            foreach (var character in value) if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
            return true;
        }
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static bool FixedTimeEqualsHex(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0; for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0;
        }
        private void BeginGameplay() { started = true; SetMenuOpen(false); }
        private void ActivateSelectedLevelOrResume()
        {
            if (selectedLevel != levelNumber)
            {
                Time.timeScale = 1f;
                PrototypeSceneNavigation.LoadPath(PrototypeCampaignProgress.ScenePathForLevel(SceneManager.GetActiveScene().path, selectedLevel, levelCount));
                return;
            }
            BeginGameplay();
        }
        private void SetMenuOpen(bool open) { MenuOpen = open; Time.timeScale = open ? 0f : 1f; }
        private static void RestartLevel() { Time.timeScale = 1f; PrototypeSceneNavigation.LoadBuildIndex(SceneManager.GetActiveScene().buildIndex); }
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };
            style.normal.textColor = Color.white;
            var controls = templateId == "first_person_action" ? "WASD move | mouse look | click/Space fire | R reload | F5 restart" :
                templateId == "platformer" ? "A/D move | Space jump" :
                templateId == "top_down_rpg" ? "WASD move | Space attack" : "W/S throttle | A/D steer";
            if (MenuOpen)
            {
                var width = 520f; var height = 285f; var left = Screen.width * 0.5f - width * 0.5f; var top = Screen.height * 0.5f - height * 0.5f;
                var title = started ? "PAUSED" : "FORGECORE " + templateId.ToUpperInvariant();
                GUI.Box(new Rect(left, top, width, height), title + "\n\n" + controls + "\n\nCampaign: level " + levelNumber + "/" + levelCount +
                    "   highest unlocked: " + HighestUnlockedLevel + "\nSelected level: " + selectedLevel +
                    "   Left/Right: select\nEnter: play/resume   R: restart   Q: quit", style);
                if (GUI.Button(new Rect(left + 52f, top + 215f, 58f, 42f), "<")) selectedLevel = Mathf.Max(1, selectedLevel - 1);
                if (GUI.Button(new Rect(left + 145f, top + 215f, 230f, 42f), selectedLevel == levelNumber && started ? "Resume" : "Play level " + selectedLevel)) ActivateSelectedLevelOrResume();
                if (GUI.Button(new Rect(left + 410f, top + 215f, 58f, 42f), ">")) selectedLevel = Mathf.Min(HighestUnlockedLevel, selectedLevel + 1);
                return;
            }
            GUI.Box(new Rect(16, 16, 500, 112), "FORGECORE " + templateId.ToUpperInvariant() + " PROTOTYPE\n" + controls +
                "\nLevel: " + levelNumber + "/" + levelCount + "   Objectives: " + CompletedObjectives + "/" + requiredObjectives + "   Health: " + Health + "   Esc: pause", style);
            if (templateId == "first_person_action" && player != null)
                GUI.Box(new Rect(Screen.width - 220, Screen.height - 86, 200, 62), player.IsReloading ? "RELOADING" : "AMMO  " + player.AmmoInMagazine + " / " + player.ReserveAmmo, style);
            if (Complete || Time.time < messageUntil) GUI.Box(new Rect(Screen.width * 0.5f - 230, 22, 460, 48), message, style);
            if (Complete && !string.IsNullOrEmpty(nextScenePath)) GUI.Box(new Rect(Screen.width * 0.5f - 230, 76, 460, 48), "Press N for next level", style);
            if (templateId == "first_person_action")
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 7, Screen.height * 0.5f - 12, 30, 30), "+", style);
            }
        }
    }
}
""";

    private const string PrototypeActorSource = """
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypeActor : MonoBehaviour
    {
        public PrototypeMode mode;
        public Transform view;
        public Transform weaponView;
        public float moveSpeed = 8f;
        public float jumpHeight = 2.2f;
        public float gravity = -26f;
        public float lookSensitivity = 2.2f;
        private CharacterController controller;
        private Vector3 spawn;
        private float verticalVelocity;
        private float yaw;
        private float pitch;
        private float vehicleSpeed;
        private const int MagazineSize = 12;
        private float nextShot;
        private float reloadUntil;
        private float recoil;
        private Vector3 weaponBase;
        private PrototypeCombatAnimation combatAnimation;
        public int AmmoInMagazine { get; private set; } = MagazineSize;
        public int ReserveAmmo { get; private set; } = 48;
        public bool IsReloading { get; private set; }
        public int ActionEvents { get; private set; }

        private void Awake() { controller = GetComponent<CharacterController>(); spawn = transform.position; yaw = transform.eulerAngles.y; }
        private void Start()
        {
            if (weaponView != null) { weaponBase = weaponView.localPosition; combatAnimation = weaponView.GetComponentInChildren<PrototypeCombatAnimation>(); }
        }
        private void Update()
        {
            var game = PrototypeGame.Instance;
            if (game != null && game.MenuOpen)
            {
                if (mode == PrototypeMode.FirstPersonAction) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                return;
            }
            if (game != null && game.Complete) return;
            if (mode == PrototypeMode.FirstPersonAction) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            var input = PrototypeInput.Read();
            switch (mode)
            {
                case PrototypeMode.FirstPersonAction: UpdateFirstPerson(input); break;
                case PrototypeMode.Platformer: UpdatePlatformer(input); break;
                case PrototypeMode.TopDownRpg: UpdateTopDown(input); break;
                case PrototypeMode.Racing: UpdateRacing(input); break;
            }
            if (transform.position.y < -15f && PrototypeGame.Instance != null) PrototypeGame.Instance.Respawn();
        }
        private void UpdateFirstPerson(PrototypeInputFrame input)
        {
            if (IsReloading && Time.time >= reloadUntil) CompleteReload();
            yaw += input.LookX * lookSensitivity; pitch = Mathf.Clamp(pitch - input.LookY * lookSensitivity, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f); if (view != null) view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            var movementInput = transform.right * input.MoveX + transform.forward * input.MoveY;
            var motion = movementInput.normalized * moveSpeed;
            ApplyGroundedMotion(motion, false);
            if (input.ReloadPressed) BeginReload();
            if (input.FireHeld && Time.time >= nextShot && TryConsumeRound())
            {
                ActionEvents++; nextShot = Time.time + 0.16f; recoil = 1f;
                var origin = view == null ? transform.position + Vector3.up : view.position;
                var direction = view == null ? transform.forward : view.forward;
                if (combatAnimation != null)
                {
                    var muzzle = combatAnimation.Muzzle;
                    var muzzleOrigin = muzzle == null ? origin + direction * 0.55f : muzzle.position;
                    RaycastHit aimHit;
                    var destination = Physics.Raycast(origin, direction, out aimHit, 80f, ~0, QueryTriggerInteraction.Ignore) ? aimHit.point : origin + direction * 80f;
                    if (Physics.Linecast(origin, muzzleOrigin, out var obstruction, ~0, QueryTriggerInteraction.Ignore))
                        PrototypeFeedback.Hit(obstruction.point, false);
                    else PrototypeProjectile.Launch(muzzleOrigin, (destination - muzzleOrigin).normalized, transform, false);
                    combatAnimation.Fire(); PrototypeFeedback.Fire(muzzleOrigin);
                }
                else
                {
                    PrototypeFeedback.Fire(origin + direction * 0.55f);
                    RaycastHit hit; if (Physics.Raycast(origin, direction, out hit, 80f))
                    { var objective = hit.collider.GetComponent<PrototypeObjective>(); if (objective != null) objective.Hit(); }
                }
                if (AmmoInMagazine == 0) BeginReload();
            }
            recoil = Mathf.MoveTowards(recoil, 0f, 8f * Time.deltaTime);
            if (weaponView != null)
            {
                var bob = movementInput.sqrMagnitude > 0.01f ? Mathf.Sin(Time.time * 9f) * 0.012f : 0f;
                weaponView.localPosition = weaponBase + new Vector3(bob, Mathf.Abs(bob) * -0.45f, -recoil * 0.08f);
                weaponView.localRotation = Quaternion.Euler(-recoil * 7f, 0f, bob * 120f);
            }
        }
        private bool TryConsumeRound()
        {
            if (IsReloading || AmmoInMagazine <= 0) return false;
            AmmoInMagazine--; return true;
        }
        private void BeginReload()
        {
            if (IsReloading || AmmoInMagazine >= MagazineSize || ReserveAmmo <= 0) return;
            IsReloading = true; reloadUntil = Time.time + 1.2f;
            if (combatAnimation != null) combatAnimation.Reload(1.2f);
        }
        private void CompleteReload()
        {
            var needed = MagazineSize - AmmoInMagazine; var loaded = Mathf.Min(needed, ReserveAmmo);
            AmmoInMagazine += loaded; ReserveAmmo -= loaded; IsReloading = false;
        }
        public void RestoreLoadout()
        {
            AmmoInMagazine = MagazineSize; ReserveAmmo = 48; IsReloading = false;
            if (combatAnimation != null) combatAnimation.ResetPose();
        }
        private void UpdatePlatformer(PrototypeInputFrame input)
        {
            var horizontal = input.MoveX; var motion = Vector3.right * horizontal * moveSpeed;
            if (horizontal != 0f) transform.forward = horizontal > 0 ? Vector3.right : Vector3.left;
            ApplyGroundedMotion(motion, input.JumpPressed);
        }
        private void UpdateTopDown(PrototypeInputFrame input)
        {
            var direction = new Vector3(input.MoveX, 0f, input.MoveY);
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 16f * Time.deltaTime);
            ApplyGroundedMotion(direction * moveSpeed, false);
            if (input.FirePressed)
            {
                ActionEvents++;
                foreach (var hit in Physics.OverlapSphere(transform.position + transform.forward * 1.5f, 2.2f))
                { var objective = hit.GetComponent<PrototypeObjective>(); if (objective != null) objective.Hit(); }
            }
        }
        private void UpdateRacing(PrototypeInputFrame input)
        {
            var throttle = input.MoveY; var steering = input.MoveX;
            if (Mathf.Abs(throttle) > 0.01f && Mathf.Abs(steering) > 0.01f) ActionEvents++;
            vehicleSpeed = Mathf.MoveTowards(vehicleSpeed, throttle * 18f, (throttle == 0f ? 10f : 7f) * Time.deltaTime);
            transform.Rotate(0f, steering * Mathf.Clamp(Mathf.Abs(vehicleSpeed), 3f, 18f) * 3.5f * Time.deltaTime, 0f);
            ApplyGroundedMotion(transform.forward * vehicleSpeed, false);
        }
        private void ApplyGroundedMotion(Vector3 planar, bool jumpPressed)
        {
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (mode == PrototypeMode.Platformer && jumpPressed)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); ActionEvents++;
                }
            }
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((planar + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
        public void TeleportToSpawn()
        {
            controller.enabled = false; transform.position = spawn; transform.rotation = Quaternion.identity; controller.enabled = true;
            verticalVelocity = 0f; vehicleSpeed = 0f;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float AutomatedMoveForVerification()
        {
            var before = transform.position;
            var motion = mode == PrototypeMode.Platformer ? Vector3.right * 2.5f :
                mode == PrototypeMode.TopDownRpg ? new Vector3(1.75f, 0f, 1.75f) : transform.forward * (mode == PrototypeMode.Racing ? 4f : 2.5f);
            controller.Move(motion);
            return Vector3.Distance(before, transform.position);
        }
        public bool IsAtSpawnForVerification() => Vector3.Distance(transform.position, spawn) < 0.05f;
        public bool AutomatedWeaponCycleForVerification()
        {
            var savedMagazine = AmmoInMagazine; var savedReserve = ReserveAmmo; var savedReload = IsReloading;
            AmmoInMagazine = MagazineSize; ReserveAmmo = 5; IsReloading = false;
            var consumed = 0; while (TryConsumeRound()) consumed++;
            var emptyBlocked = !TryConsumeRound(); CompleteReload();
            var passed = consumed == MagazineSize && emptyBlocked && AmmoInMagazine == 5 && ReserveAmmo == 0;
            AmmoInMagazine = savedMagazine; ReserveAmmo = savedReserve; IsReloading = savedReload; return passed;
        }
#endif
    }
}
""";

    private const string PrototypeObjectiveSource = """
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    public sealed class PrototypeObjective : MonoBehaviour
    {
        public ObjectiveKind kind;
        public int order;
        public int health = 1;
        public int archetype;
        private bool consumed;
        private Vector3 home;
        private Vector3 baseScale;
        private float nextContactDamage;
        private float flashUntil;
        private PrototypeRangedEnemy rangedEnemy;
        private void Start()
        {
            home = transform.position;
            rangedEnemy = GetComponent<PrototypeRangedEnemy>();
            var game = PrototypeGame.Instance;
            if (kind == ObjectiveKind.Target && game != null && game.templateId is "first_person_action" or "top_down_rpg")
            {
                archetype = Mathf.Clamp(archetype, 0, 2); health = Mathf.Max(health, HealthFor(archetype, game.levelNumber));
                transform.localScale *= archetype == 0 ? 0.82f : archetype == 2 ? 1.28f : 1f;
            }
            baseScale = transform.localScale;
        }
        public static int HealthFor(int profile, int level) => Mathf.Max(1, 2 + (Mathf.Max(1, level) - 1) / 2 + (Mathf.Clamp(profile, 0, 2) - 1));
        public static float MoveSpeedFor(int profile, int level)
        {
            var baseSpeed = Mathf.Clamp(profile, 0, 2) == 0 ? 2.25f : Mathf.Clamp(profile, 0, 2) == 2 ? 1.25f : 1.7f;
            return baseSpeed + Mathf.Max(0, level - 1) * 0.1f;
        }
        public static int ContactDamageFor(int profile, int level) => 8 + Mathf.Max(1, level) * 2 + Mathf.Clamp(profile, 0, 2) * 3;
        public void Hit()
        {
            if (consumed || kind != ObjectiveKind.Target) return;
            health--; flashUntil = Time.time + 0.12f; PrototypeFeedback.Hit(transform.position, health <= 0); if (health <= 0) Consume();
        }
        private void OnTriggerEnter(Collider other)
        {
            if (consumed || other.GetComponent<PrototypeActor>() == null) return;
            if (kind == ObjectiveKind.Hazard) { if (PrototypeGame.Instance != null) PrototypeGame.Instance.Damage(25); return; }
            if (kind is ObjectiveKind.Collectible or ObjectiveKind.Checkpoint or ObjectiveKind.Finish) Consume();
        }
        private void Consume()
        {
            if (PrototypeGame.Instance == null || !PrototypeGame.Instance.Register(kind, order)) return;
            if (kind != ObjectiveKind.Target) PrototypeFeedback.Objective(transform.position, kind);
            consumed = true; gameObject.SetActive(false);
        }
        private void Update()
        {
            if (consumed) return;
            if (kind == ObjectiveKind.Collectible) { transform.Rotate(0f, 60f * Time.deltaTime, 0f); return; }
            if (kind != ObjectiveKind.Target) return;
            if (rangedEnemy != null) return; // Ranged controller owns movement, aim, fire and reload for the new rig.
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            transform.localScale = baseScale * (Time.time < flashUntil ? 1.12f : 1f);
            var game = PrototypeGame.Instance;
            if (game == null || game.player == null || game.Complete || game.MenuOpen || game.templateId is not ("first_person_action" or "top_down_rpg"))
            { transform.Rotate(0f, 35f * Time.deltaTime, 0f); return; }
            var offset = game.player.transform.position - transform.position; offset.y = 0f;
            var distance = offset.magnitude; var chaseRange = 16f + game.levelNumber * 2f;
            if (distance <= chaseRange && distance > 1.15f)
            {
                var direction = offset / distance;
                transform.position += direction * MoveSpeedFor(archetype, game.levelNumber) * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
            }
            else if (distance > chaseRange * 1.4f)
                transform.position = Vector3.MoveTowards(transform.position, home, 1.2f * Time.deltaTime);
            if (distance <= 1.45f && Time.time >= nextContactDamage)
            {
                nextContactDamage = Time.time + 1.25f;
                game.Damage(ContactDamageFor(archetype, game.levelNumber));
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool AutomatedInteractForVerification()
        {
            if (kind == ObjectiveKind.Target)
            {
                for (var attempt = 0; attempt < 20 && !consumed; attempt++) Hit();
            }
            else Consume();
            return consumed;
        }
#endif
    }
}
""";

    private const string PrototypeCameraSource = """
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    public sealed class PrototypeCamera : MonoBehaviour
    {
        public PrototypeMode mode;
        public Transform target;
        private Vector3 velocity;
        private Camera view;
        private void Start()
        {
            view = GetComponent<Camera>(); ConfigureProjection(); SnapToTarget();
        }
        private void ConfigureProjection()
        {
            if (view == null) return;
            view.orthographic = mode is PrototypeMode.Platformer or PrototypeMode.TopDownRpg;
            view.orthographicSize = mode == PrototypeMode.Platformer ? 7.5f : 14f;
            view.fieldOfView = mode == PrototypeMode.Racing ? 68f : 60f;
            view.nearClipPlane = 0.1f; view.farClipPlane = 120f;
        }
        private Vector3 DesiredPosition()
        {
            if (mode == PrototypeMode.Platformer) return new Vector3(target.position.x, target.position.y + 3.5f, -16f);
            if (mode == PrototypeMode.TopDownRpg) return target.position + new Vector3(0f, 20f, -10f);
            return target.position + target.rotation * new Vector3(0f, 6f, -13f);
        }
        private void AimAtTarget()
        {
            var height = mode == PrototypeMode.Platformer ? 1f : 0.8f;
            transform.LookAt(target.position + Vector3.up * height);
        }
        private void SnapToTarget()
        {
            if (target == null) return;
            transform.position = DesiredPosition(); AimAtTarget(); velocity = Vector3.zero;
        }
        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, 0.12f);
            AimAtTarget();
        }
    }
}
""";

    private const string EditorSource = """
using System;
using System.Collections.Generic;
using System.IO;
using ForgeCoreGameTemplates;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCorePrototypeBuilder
    {
        private static readonly HashSet<string> Templates = new HashSet<string>(StringComparer.Ordinal)
        { "first_person_action", "platformer", "top_down_rpg", "racing" };

        public static string Build(string scenePath, string templateId, int seed)
        {
            return BuildLevel(scenePath, templateId, seed, "", 1, 1);
        }

        public static string BuildCampaign(string firstScenePath, string templateId, int seed, int levelCount)
        {
            if (!Templates.Contains(templateId)) throw new InvalidOperationException("Unsupported game template.");
            if (seed < 1 || seed > 999999 || levelCount < 1 || levelCount > 12) throw new InvalidOperationException("Campaign seed or level count is outside the approved range.");
            var scenePaths = CampaignScenePaths(firstScenePath, levelCount);
            foreach (var path in scenePaths)
                if (File.Exists(Path.GetFullPath(Path.Combine(ProjectRoot(), path)))) throw new InvalidOperationException("Campaign scene already exists; ForgeCore never overwrites scenes.");
            var priorBuildSettings = EditorBuildSettings.scenes; var created = new List<string>();
            try
            {
                for (var index = 0; index < scenePaths.Count; index++)
                {
                    created.Add(scenePaths[index]);
                    var next = index + 1 < scenePaths.Count ? scenePaths[index + 1] : "";
                    var levelSeed = ((seed - 1 + index) % 999999) + 1;
                    BuildLevel(scenePaths[index], templateId, levelSeed, next, index + 1, levelCount);
                }
                return "Created " + levelCount + " linked " + templateId + " campaign levels from " + firstScenePath + " with base seed " + seed + ".";
            }
            catch
            {
                foreach (var path in created) if (File.Exists(Path.GetFullPath(Path.Combine(ProjectRoot(), path)))) AssetDatabase.DeleteAsset(path);
                EditorBuildSettings.scenes = priorBuildSettings;
                throw;
            }
        }

        private static string BuildLevel(string scenePath, string templateId, int seed, string nextScenePath, int levelNumber, int levelCount)
        {
            if (!Templates.Contains(templateId)) throw new InvalidOperationException("Unsupported game template.");
            if (seed < 1 || seed > 999999) throw new InvalidOperationException("Seed is outside the approved range.");
            var absolute = Path.GetFullPath(Path.Combine(ProjectRoot(), scenePath));
            if (File.Exists(absolute)) throw new InvalidOperationException("Scene already exists; ForgeCore never overwrites scenes.");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            var previous = UnityEngine.Random.state; UnityEngine.Random.InitState(seed);
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.4f);
                var light = new GameObject("Directional Light"); light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                var lamp = light.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.15f;
                var root = new GameObject("ForgeCore Prototype"); var game = root.AddComponent<PrototypeGame>(); game.templateId = templateId;
                game.levelNumber = levelNumber; game.levelCount = levelCount; game.nextScenePath = nextScenePath;
                if (templateId == "first_person_action") BuildFirstPerson(game);
                else if (templateId == "platformer") BuildPlatformer(game);
                else if (templateId == "top_down_rpg") BuildTopDown(game);
                else BuildRacing(game);
                ValidateGameplayContract(scene, templateId, game);
                ValidateMonoScriptAssets(scene);
                if (!EditorSceneManager.SaveScene(scene, scenePath, false)) throw new InvalidOperationException("Unity could not save the prototype scene.");
                var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes); var registered = false;
                foreach (var item in scenes) if (string.Equals(item.path, scenePath, StringComparison.Ordinal)) { registered = true; break; }
                if (!registered) { scenes.Add(new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
                return "Created " + templateId + " prototype at " + scenePath + " with seed " + seed + ".";
            }
            finally { UnityEngine.Random.state = previous; }
        }

        private static void ValidateMonoScriptAssets(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) throw new InvalidOperationException("Prototype contains a missing MonoBehaviour reference.");
                var script = MonoScript.FromMonoBehaviour(component);
                var path = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
                if (string.IsNullOrEmpty(path) || !string.Equals(Path.GetFileNameWithoutExtension(path), component.GetType().Name, StringComparison.Ordinal))
                    throw new InvalidOperationException("Prototype MonoBehaviour '" + component.GetType().FullName + "' does not have a matching script asset; standalone serialization is blocked.");
            }
        }

        private static void ValidateGameplayContract(Scene scene, string templateId, PrototypeGame game)
        {
            var games = new List<PrototypeGame>(); var actors = new List<PrototypeActor>();
            var objectives = new List<PrototypeObjective>(); var cameras = new List<Camera>();
            foreach (var root in scene.GetRootGameObjects())
            {
                games.AddRange(root.GetComponentsInChildren<PrototypeGame>(true));
                actors.AddRange(root.GetComponentsInChildren<PrototypeActor>(true));
                objectives.AddRange(root.GetComponentsInChildren<PrototypeObjective>(true));
                cameras.AddRange(root.GetComponentsInChildren<Camera>(true));
            }
            if (games.Count != 1 || games[0] != game) throw new InvalidOperationException("Prototype must contain exactly one game-state controller.");
            if (!string.Equals(game.templateId, templateId, StringComparison.Ordinal)) throw new InvalidOperationException("Prototype game-state template id does not match the requested template.");
            if (game.levelNumber < 1 || game.levelCount < game.levelNumber || game.levelCount > 12)
                throw new InvalidOperationException("Prototype level position is outside the reviewed campaign range.");
            if (game.levelNumber < game.levelCount && string.IsNullOrEmpty(game.nextScenePath))
                throw new InvalidOperationException("Non-final campaign levels require a next-scene transition.");
            if (game.levelNumber == game.levelCount && !string.IsNullOrEmpty(game.nextScenePath))
                throw new InvalidOperationException("Final campaign levels cannot point beyond the campaign.");
            if (actors.Count != 1 || game.player == null || actors[0] != game.player) throw new InvalidOperationException("Prototype must contain exactly one assigned player actor.");
            if (game.player.GetComponent<CharacterController>() == null) throw new InvalidOperationException("Prototype player requires a CharacterController.");
            if (cameras.Count != 1 || !cameras[0].CompareTag("MainCamera")) throw new InvalidOperationException("Prototype must contain exactly one tagged Main Camera.");

            var expectedMode = templateId == "first_person_action" ? PrototypeMode.FirstPersonAction :
                templateId == "platformer" ? PrototypeMode.Platformer :
                templateId == "top_down_rpg" ? PrototypeMode.TopDownRpg : PrototypeMode.Racing;
            var expectedKind = templateId == "platformer" ? ObjectiveKind.Collectible :
                templateId == "racing" ? ObjectiveKind.Checkpoint : ObjectiveKind.Target;
            var expectedCount = templateId == "platformer" ? 5 : templateId == "top_down_rpg" ? 7 : 6;
            if (game.player.mode != expectedMode) throw new InvalidOperationException("Prototype player mode does not match the requested template.");
            if (game.requiredObjectives != expectedCount || objectives.Count != expectedCount)
                throw new InvalidOperationException("Prototype objective count does not match its reviewed gameplay contract.");

            var orders = new HashSet<int>();
            foreach (var objective in objectives)
            {
                if (objective.kind != expectedKind) throw new InvalidOperationException("Prototype contains an objective kind outside its reviewed gameplay contract.");
                if (!orders.Add(objective.order) || objective.order < 0 || objective.order >= expectedCount)
                    throw new InvalidOperationException("Prototype objective order must be unique and contiguous.");
                if (expectedKind == ObjectiveKind.Target && objective.archetype != objective.order % 3)
                    throw new InvalidOperationException("Prototype target archetypes must follow the reviewed runner/walker/brute sequence.");
                if (expectedKind == ObjectiveKind.Target && objective.GetComponentsInChildren<Renderer>(true).Length < 4)
                    throw new InvalidOperationException("Prototype targets require the reviewed articulated silhouette parts.");
                var collider = objective.GetComponent<Collider>();
                if (collider == null || collider.isTrigger == (expectedKind == ObjectiveKind.Target))
                    throw new InvalidOperationException("Prototype objective collider does not match its interaction mode.");
                if (expectedKind != ObjectiveKind.Target)
                {
                    var body = objective.GetComponent<Rigidbody>();
                    if (body == null || !body.isKinematic || body.useGravity)
                        throw new InvalidOperationException("Trigger objectives require a non-gravity kinematic Rigidbody.");
                }
            }
            for (var order = 0; order < expectedCount; order++)
                if (!orders.Contains(order)) throw new InvalidOperationException("Prototype objective order is not contiguous.");

            if (expectedMode == PrototypeMode.FirstPersonAction)
            {
                if (game.player.view == null || game.player.view.GetComponent<Camera>() != cameras[0] || !game.player.view.IsChildOf(game.player.transform))
                    throw new InvalidOperationException("First-person prototypes require an assigned child view camera.");
                if (game.player.weaponView == null || !game.player.weaponView.IsChildOf(game.player.view) || game.player.weaponView.GetComponent<Collider>() != null ||
                    game.player.weaponView.GetComponentsInChildren<Renderer>(true).Length < 4)
                    throw new InvalidOperationException("First-person prototypes require a non-colliding multi-part view weapon attached to the player camera.");
            }
            else
            {
                var follow = cameras[0].GetComponent<PrototypeCamera>();
                if (follow == null || follow.target != game.player.transform || follow.mode != expectedMode)
                    throw new InvalidOperationException("Prototype follow camera does not match its player mode.");
            }

            if (expectedKind == ObjectiveKind.Checkpoint && game.Register(ObjectiveKind.Checkpoint, 1))
                throw new InvalidOperationException("Racing checkpoints accepted an out-of-order completion.");
            for (var order = 0; order < expectedCount; order++)
            {
                if (!game.Register(expectedKind, order)) throw new InvalidOperationException("Prototype objective loop rejected a valid completion.");
                if (order + 1 < expectedCount && game.Complete) throw new InvalidOperationException("Prototype completed before every required objective.");
            }
            if (!game.Complete || game.CompletedObjectives != expectedCount)
                throw new InvalidOperationException("Prototype objective loop did not reach its completion state.");
            if (game.Register(expectedKind, 0)) throw new InvalidOperationException("Completed prototype accepted an additional objective.");
        }

        private static void BuildFirstPerson(PrototypeGame game)
        {
            ForgeCoreIndustrialYard.Build(game);
            var actor = Player(PrototypeMode.FirstPersonAction, new Vector3(0f, 1f, -10f)); game.player = actor;
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(actor.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.65f, 0f); cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>(); actor.view = cameraObject.transform;
            var weapon = new GameObject("View Weapon Rig"); weapon.transform.SetParent(cameraObject.transform, false);
            weapon.transform.localPosition = new Vector3(0.27f, -0.24f, 0.45f); actor.weaponView = weapon.transform;
            var pose = new GameObject("Weapon Motion"); pose.transform.SetParent(weapon.transform, false);
            ForgeCoreIndustrialYard.BuildWeapon(pose.transform);
            pose.AddComponent<PrototypeCombatAnimation>().firstPerson = true;
            var angleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f); var radius = UnityEngine.Random.Range(9.5f, 12.5f);
            for (var i = 0; i < 6; i++)
            {
                var profile = i % 3; var label = profile == 0 ? "Runner " : profile == 2 ? "Brute " : "Walker ";
                var angle = angleOffset + i / 6f * Mathf.PI * 2f; Objective(label + (i + 1), ObjectiveKind.Target, i,
                    new Vector3(Mathf.Sin(angle) * radius, 1f, Mathf.Cos(angle) * radius), PrimitiveType.Capsule, Vector3.one, true);
            }
            game.requiredObjectives = 6;
        }

        private static void BuildPlatformer(PrototypeGame game)
        {
            Floor(new Vector3(0f, -1f, 0f), new Vector3(48f, 1f, 8f));
            var actor = Player(PrototypeMode.Platformer, new Vector3(-20f, 1f, 0f)); game.player = actor; FollowCamera(actor, PrototypeMode.Platformer);
            var variation = UnityEngine.Random.Range(-0.8f, 0.8f);
            var positions = new[] { new Vector3(-12f, 1f, 0f), new Vector3(-5f, 3f + variation, 0f), new Vector3(3f, 5f - variation, 0f), new Vector3(11f, 3f + variation * 0.5f, 0f), new Vector3(18f, 1f, 0f) };
            for (var i = 0; i < positions.Length; i++)
            {
                Block("Platform " + (i + 1), positions[i] - Vector3.up, new Vector3(5f, 1f, 5f));
                Objective("Crystal " + (i + 1), ObjectiveKind.Collectible, i, positions[i] + Vector3.up * 1.2f, PrimitiveType.Sphere, Vector3.one * 0.75f);
            }
            game.requiredObjectives = positions.Length;
        }

        private static void BuildTopDown(PrototypeGame game)
        {
            Floor(new Vector3(0f, -0.5f, 0f), new Vector3(34f, 1f, 34f));
            var actor = Player(PrototypeMode.TopDownRpg, new Vector3(0f, 1f, -11f)); game.player = actor; FollowCamera(actor, PrototypeMode.TopDownRpg);
            var angleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (var i = 0; i < 7; i++)
            {
                var angle = angleOffset + i / 7f * Mathf.PI * 2f; var radius = 7f + (i % 2) * 4f + UnityEngine.Random.Range(-0.6f, 0.6f);
                var profile = i % 3; var label = profile == 0 ? "Runner " : profile == 2 ? "Brute " : "Walker ";
                Objective(label + (i + 1), ObjectiveKind.Target, i, new Vector3(Mathf.Sin(angle) * radius, 1f, Mathf.Cos(angle) * radius),
                    PrimitiveType.Cube, new Vector3(1.4f, 2f, 1.4f));
            }
            game.requiredObjectives = 7;
        }

        private static void BuildRacing(PrototypeGame game)
        {
            Floor(new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));
            var actor = Player(PrototypeMode.Racing, new Vector3(0f, 1f, -22f)); actor.transform.localScale = new Vector3(1.4f, 0.75f, 2.2f);
            game.player = actor; FollowCamera(actor, PrototypeMode.Racing);
            var rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-25f, 25f), 0f);
            var checkpoints = new[] { new Vector3(18f, 1f, -18f), new Vector3(22f, 1f, 10f), new Vector3(0f, 1f, 23f), new Vector3(-22f, 1f, 10f), new Vector3(-18f, 1f, -18f), new Vector3(0f, 1f, -24f) };
            for (var i = 0; i < checkpoints.Length; i++)
                CheckpointGate("Checkpoint " + (i + 1), i, rotation * checkpoints[i], rotation);
            game.requiredObjectives = checkpoints.Length;
        }

        private static PrototypeActor Player(PrototypeMode mode, Vector3 position)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Capsule); value.name = mode == PrototypeMode.Racing ? "Player Vehicle" : "Player"; value.transform.position = position;
            var old = value.GetComponent<Collider>(); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var controller = value.AddComponent<CharacterController>(); controller.height = 2f; controller.radius = 0.5f; controller.center = Vector3.zero;
            var actor = value.AddComponent<PrototypeActor>(); actor.mode = mode; return actor;
        }
        private static void FollowCamera(PrototypeActor actor, PrototypeMode mode)
        {
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera"; cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<PrototypeCamera>(); follow.target = actor.transform; follow.mode = mode;
        }
        private static void Objective(string name, ObjectiveKind kind, int order, Vector3 position, PrimitiveType primitive, Vector3 scale, bool yardActor = false)
        {
            var value = GameObject.CreatePrimitive(primitive); value.name = name; value.transform.position = position; value.transform.localScale = scale;
            var collider = value.GetComponent<Collider>(); collider.isTrigger = kind != ObjectiveKind.Target;
            if (collider.isTrigger) { var body = value.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; }
            var objective = value.AddComponent<PrototypeObjective>(); objective.kind = kind; objective.order = order; objective.archetype = kind == ObjectiveKind.Target ? order % 3 : 0;
            if (yardActor) { ForgeCoreIndustrialYard.BuildTarget(value.transform); return; }
            if (kind == ObjectiveKind.Target)
            {
                VisualPart(value.transform, "Target Head", PrimitiveType.Sphere, new Vector3(0f, 0.72f, 0f), new Vector3(0.55f, 0.45f, 0.55f), Quaternion.identity);
                VisualPart(value.transform, "Target Left Arm", PrimitiveType.Cube, new Vector3(-0.58f, 0.05f, 0f), new Vector3(0.24f, 0.82f, 0.24f), Quaternion.Euler(0f, 0f, -9f));
                VisualPart(value.transform, "Target Right Arm", PrimitiveType.Cube, new Vector3(0.58f, 0.05f, 0f), new Vector3(0.24f, 0.82f, 0.24f), Quaternion.Euler(0f, 0f, 9f));
            }
        }
        private static void VisualPart(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 scale, Quaternion localRotation)
        {
            var part = GameObject.CreatePrimitive(primitive); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition; part.transform.localScale = scale; part.transform.localRotation = localRotation;
            var collider = part.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }
        private static void PointLight(string name, Vector3 position, Color color)
        {
            var value = new GameObject(name); value.transform.position = position; var light = value.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.range = 16f; light.intensity = 3.2f; light.shadows = LightShadows.Soft;
        }
        private static void CheckpointGate(string name, int order, Vector3 position, Quaternion rotation)
        {
            var gate = new GameObject(name); gate.transform.position = position; gate.transform.rotation = rotation;
            var collider = gate.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(7f, 3.5f, 1f);
            var body = gate.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var objective = gate.AddComponent<PrototypeObjective>(); objective.kind = ObjectiveKind.Checkpoint; objective.order = order;
            GatePart(gate.transform, "Left Pylon", new Vector3(-3.15f, 0.5f, 0f), new Vector3(0.45f, 3f, 0.55f));
            GatePart(gate.transform, "Right Pylon", new Vector3(3.15f, 0.5f, 0f), new Vector3(0.45f, 3f, 0.55f));
            GatePart(gate.transform, "Top Marker", new Vector3(0f, 2f, 0f), new Vector3(6.75f, 0.35f, 0.55f));
        }
        private static void GatePart(Transform parent, string name, Vector3 localPosition, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition; part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }
        private static void Floor(Vector3 position, Vector3 scale) { Block("Ground", position, scale); }
        private static void Wall(Vector3 position, Vector3 scale) { Block("Arena Wall", position, scale); }
        private static void Block(string name, Vector3 position, Vector3 scale)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name; value.transform.position = position; value.transform.localScale = scale;
        }
        private static void RotatedBlock(string name, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name; value.transform.position = position; value.transform.localScale = scale; value.transform.rotation = rotation;
        }
        private static List<string> CampaignScenePaths(string firstScenePath, int levelCount)
        {
            var normalized = (firstScenePath ?? "").Replace('\\', '/'); var slash = normalized.LastIndexOf('/');
            if (slash < 7 || !normalized.EndsWith("-Level01.unity", StringComparison.Ordinal))
                throw new InvalidOperationException("Campaign first scene must end with -Level01.unity under Assets.");
            var directory = normalized.Substring(0, slash + 1); var file = normalized.Substring(slash + 1);
            var stem = file.Substring(0, file.Length - "01.unity".Length); var paths = new List<string>();
            for (var index = 1; index <= levelCount; index++) paths.Add(directory + stem + index.ToString("00") + ".unity");
            return paths;
        }
        private static string ProjectRoot() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
    }
}
""";
}
