using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Core;
using Game.Features.Vision.Data;
using Game.Features.Vision.Interfaces;

namespace Game.Features.Vision.Tests
{
    /// <summary>
    /// Unit tests for VisionSystem core logic.
    /// [TASK-006] Integration and testing suite
    /// </summary>
    [TestFixture]
    public class VisionSystemTests
    {
        private VisionSystem _visionSystem;
        private VisionConfig _visionConfig;

        [SetUp]
        public void Setup()
        {
            // [006-G] Create test config
            _visionConfig = ScriptableObject.CreateInstance<VisionConfig>();
            
            // [006-G] Create vision system
            _visionSystem = new VisionSystem();
            _visionSystem.Initialize(_visionConfig);
        }

        [TearDown]
        public void TearDown()
        {
            // [006-G] Cleanup
            if (_visionConfig != null)
                Object.DestroyImmediate(_visionConfig);
        }

        /// <summary>
        /// Test that VisionSystem initializes correctly.
        /// [006-G] Validates basic setup
        /// </summary>
        [Test]
        public void Initialize_WithValidConfig_DoesNotThrow()
        {
            var config = ScriptableObject.CreateInstance<VisionConfig>();
            var system = new VisionSystem();
            
            // Should not throw
            Assert.DoesNotThrow(() => system.Initialize(config));
            
            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// Test that VisionSystem throws on null config.
        /// [006-G] Error handling validation
        /// </summary>
        [Test]
        public void Initialize_WithNullConfig_ThrowsArgumentNullException()
        {
            var system = new VisionSystem();
            
            // Should throw ArgumentNullException
            Assert.Throws<System.ArgumentNullException>(() => system.Initialize(null));
        }

        /// <summary>
        /// Test that GetVisibleObjects returns empty list if not initialized.
        /// [006-G] Null safety check
        /// </summary>
        [Test]
        public void GetVisibleObjects_BeforeInitialize_ReturnsEmptyList()
        {
            var uninitializedSystem = new VisionSystem();
            
            var result = uninitializedSystem.GetVisibleObjects();
            
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Test that GetModel returns null if not initialized.
        /// [006-G] Null safety check
        /// </summary>
        [Test]
        public void GetModel_BeforeInitialize_ReturnsNull()
        {
            var uninitializedSystem = new VisionSystem();
            
            var result = uninitializedSystem.GetModel();
            
            Assert.IsNull(result);
        }

        /// <summary>
        /// Test that MergeVisionResults combines two lists without duplicates.
        /// [006-G] Vision merging validation - TASK-004-A
        /// </summary>
        [Test]
        public void MergeVisionResults_WithUniqueItems_CombinesLists()
        {
            // Create test colliders (using mock objects)
            var playerVision = new List<Collider>();
            var companionVision = new List<Collider>();
            
            // Create dummy GameObjects with colliders
            var obj1 = new GameObject("TestObject1");
            var collider1 = obj1.AddComponent<BoxCollider>();
            playerVision.Add(collider1);
            
            var obj2 = new GameObject("TestObject2");
            var collider2 = obj2.AddComponent<BoxCollider>();
            companionVision.Add(collider2);
            
            // Act
            var merged = VisionSystem.MergeVisionResults(playerVision, companionVision);
            
            // Assert
            Assert.AreEqual(2, merged.Count);
            Assert.Contains(collider1, merged);
            Assert.Contains(collider2, merged);
            
            // Cleanup
            Object.DestroyImmediate(obj1);
            Object.DestroyImmediate(obj2);
        }

        /// <summary>
        /// Test that MergeVisionResults avoids duplicates.
        /// [006-G] Duplicate prevention validation - TASK-004-A
        /// </summary>
        [Test]
        public void MergeVisionResults_WithDuplicates_NoDuplicatesInResult()
        {
            // Create test collider
            var testObj = new GameObject("TestObject");
            var collider = testObj.AddComponent<BoxCollider>();
            
            var playerVision = new List<Collider> { collider };
            var companionVision = new List<Collider> { collider };
            
            // Act
            var merged = VisionSystem.MergeVisionResults(playerVision, companionVision);
            
            // Assert - should have only 1, not 2
            Assert.AreEqual(1, merged.Count);
            
            // Cleanup
            Object.DestroyImmediate(testObj);
        }

        /// <summary>
        /// Test that MergeVisionResults handles null lists safely.
        /// [006-G] Null safety check - TASK-004-A
        /// </summary>
        [Test]
        public void MergeVisionResults_WithNullLists_ReturnsEmptyList()
        {
            // Act
            var merged = VisionSystem.MergeVisionResults(null, null);
            
            // Assert
            Assert.IsNotNull(merged);
            Assert.AreEqual(0, merged.Count);
        }

        /// <summary>
        /// Test that model position updates correctly.
        /// [006-G] Model state validation
        /// </summary>
        [Test]
        public void UpdateVisionFromPosition_UpdatesModelPosition()
        {
            // Create a test position
            Vector3 testPosition = new Vector3(5f, 0f, 10f);
            
            // Act
            _visionSystem.UpdateVisionFromPosition(testPosition, 20f);
            
            // Assert
            var model = _visionSystem.GetModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(testPosition, model.Position);
        }

        /// <summary>
        /// Test that model vision range updates correctly.
        /// [006-G] Model state validation
        /// </summary>
        [Test]
        public void UpdateVisionFromPosition_UpdatesModelRange()
        {
            // Act
            _visionSystem.UpdateVisionFromPosition(Vector3.zero, 25f);
            
            // Assert
            var model = _visionSystem.GetModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(25f, model.VisionRange);
        }

        /// <summary>
        /// Test that OnVisibleObjectsChanged event is triggered.
        /// [006-G] Event system validation
        /// </summary>
        [Test]
        public void UpdateVisionFromPosition_TriggersOnVisibleObjectsChangedEvent()
        {
            // Setup event tracking
            bool eventFired = false;
            List<Collider> eventData = null;
            
            _visionSystem.OnVisibleObjectsChanged += (data) =>
            {
                eventFired = true;
                eventData = data;
            };
            
            // Act
            _visionSystem.UpdateVisionFromPosition(Vector3.zero, 20f);
            
            // Assert
            Assert.IsTrue(eventFired, "OnVisibleObjectsChanged event should fire");
            Assert.IsNotNull(eventData);
        }

        /// <summary>
        /// Test PublishMergedVision triggers OnMergedVisionChanged.
        /// [006-G] Event system validation - TASK-004-B
        /// </summary>
        [Test]
        public void PublishMergedVision_TriggersOnMergedVisionChangedEvent()
        {
            // Setup event tracking
            bool eventFired = false;
            List<Collider> eventData = null;
            
            _visionSystem.OnMergedVisionChanged += (data) =>
            {
                eventFired = true;
                eventData = data;
            };
            
            // Create test data
            var testObj = new GameObject("TestObject");
            var collider = testObj.AddComponent<BoxCollider>();
            var mergedList = new List<Collider> { collider };
            
            // Act
            _visionSystem.PublishMergedVision(mergedList);
            
            // Assert
            Assert.IsTrue(eventFired, "OnMergedVisionChanged event should fire");
            Assert.IsNotNull(eventData);
            Assert.AreEqual(1, eventData.Count);
            
            // Cleanup
            Object.DestroyImmediate(testObj);
        }

        /// <summary>
        /// Test that vision system handles multiple positions.
        /// [006-G] Stress testing - multiple updates
        /// </summary>
        [Test]
        public void UpdateVisionFromPosition_MultipleUpdates_LastPositionWins()
        {
            // Act - multiple updates
            _visionSystem.UpdateVisionFromPosition(new Vector3(1, 0, 1), 20f);
            _visionSystem.UpdateVisionFromPosition(new Vector3(5, 0, 5), 20f);
            _visionSystem.UpdateVisionFromPosition(new Vector3(10, 0, 10), 20f);
            
            // Assert - last update wins
            var model = _visionSystem.GetModel();
            Assert.AreEqual(new Vector3(10, 0, 10), model.Position);
        }

        /// <summary>
        /// Test companion vision range (should be different from player).
        /// [006-G] Companion vision validation - TASK-003
        /// </summary>
        [Test]
        public void VisionConfig_CompanionRangeIsSmaller_ThanPlayerRange()
        {
            // Assert - companion range (8) < player range (20)
            Assert.Less(_visionConfig.CompanionVisionRange, _visionConfig.PlayerVisionRange);
            Assert.AreEqual(8f, _visionConfig.CompanionVisionRange);
            Assert.AreEqual(20f, _visionConfig.PlayerVisionRange);
        }

        /// <summary>
        /// Test fade distance configuration.
        /// [006-G] Fade effect config validation - TASK-005
        /// </summary>
        [Test]
        public void VisionConfig_FadeDistances_AreConfigured()
        {
            // Assert - fade distances should be set
            Assert.Greater(_visionConfig.FadeStartDistance, 0);
            Assert.Greater(_visionConfig.FadeCompleteDistance, _visionConfig.FadeStartDistance);
            Assert.AreEqual(18f, _visionConfig.FadeStartDistance);
            Assert.AreEqual(25f, _visionConfig.FadeCompleteDistance);
        }
    }

    /// <summary>
    /// Integration tests for complete vision system.
    /// [TASK-006] Full system integration validation
    /// </summary>
    [TestFixture]
    public class VisionSystemIntegrationTests
    {
        /// <summary>
        /// Test that multiple vision systems can coexist.
        /// [006-H] Multi-entity support - TASK-003
        /// </summary>
        [Test]
        public void MultipleVisionSystems_CanCoexist_WithoutInterference()
        {
            var config = ScriptableObject.CreateInstance<VisionConfig>();
            
            // Create two independent vision systems
            var playerVision = new VisionSystem();
            var companionVision = new VisionSystem();
            
            playerVision.Initialize(config);
            companionVision.Initialize(config);
            
            // Update each system
            playerVision.UpdateVisionFromPosition(Vector3.zero, config.PlayerVisionRange);
            companionVision.UpdateVisionFromPosition(Vector3.one, config.CompanionVisionRange);
            
            // Get models
            var playerModel = playerVision.GetModel();
            var companionModel = companionVision.GetModel();
            
            // Assert - each system maintains independent state
            Assert.AreEqual(Vector3.zero, playerModel.Position);
            Assert.AreEqual(Vector3.one, companionModel.Position);
            Assert.AreEqual(config.PlayerVisionRange, playerModel.VisionRange);
            Assert.AreEqual(config.CompanionVisionRange, companionModel.VisionRange);
            
            // Cleanup
            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// Test merging of player and companion vision.
        /// [006-H] Vision sharing validation - TASK-004
        /// </summary>
        [Test]
        public void VisionMerging_CombinesPlayerAndCompanion_Correctly()
        {
            // Create test objects
            var testObj1 = new GameObject("PlayerVisionObj");
            var collider1 = testObj1.AddComponent<BoxCollider>();
            
            var testObj2 = new GameObject("CompanionVisionObj");
            var collider2 = testObj2.AddComponent<BoxCollider>();
            
            var playerVisible = new List<Collider> { collider1 };
            var companionVisible = new List<Collider> { collider2 };
            
            // Act
            var merged = VisionSystem.MergeVisionResults(playerVisible, companionVisible);
            
            // Assert
            Assert.AreEqual(2, merged.Count);
            Assert.IsTrue(merged.Contains(collider1));
            Assert.IsTrue(merged.Contains(collider2));
            
            // Cleanup
            Object.DestroyImmediate(testObj1);
            Object.DestroyImmediate(testObj2);
        }
    }
}
