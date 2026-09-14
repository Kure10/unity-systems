using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace LightweightDI.Tests
{
    /// <summary>
    /// The container has no Unity dependency beyond logging, so it is testable in plain
    /// edit-mode tests - no scene, no play mode, no fixtures.
    /// </summary>
    public class InjectorTests
    {
        private Injector _injector;

        [SetUp]
        public void SetUp()
        {
            _injector = new Injector();
        }

        #region Test doubles

        private class ServiceA
        {
            public int Value = 42;
        }

        private class ServiceB
        {
        }

        private interface IGreeter
        {
            string Greet();
        }

        private class EnglishGreeter : IGreeter
        {
            public string Greet() => "hello";
        }

        private class Consumer
        {
            [Inject] private ServiceA _privateField;
            [Inject] public ServiceA PublicProperty { get; set; }

            public ServiceA PrivateField => _privateField;
        }

        private class ConsumerBase
        {
            [Inject] protected ServiceA _inherited;

            public ServiceA Inherited => _inherited;
        }

        private class DerivedConsumer : ConsumerBase
        {
        }

        private class TrackedConsumer : IInjectable
        {
            public bool Injected { get; set; }

            [Inject] public ServiceA Service;
        }

        private class CountingManager : IManager
        {
            public int InitializeCalls;

            [Inject] public ServiceA Service;

            public void Initialize()
            {
                InitializeCalls++;
            }
        }

        #endregion

        [Test]
        public void MapValue_ResolvesIntoPrivateFieldAndProperty()
        {
            var service = new ServiceA();
            _injector.MapValue<ServiceA>(service);

            var consumer = new Consumer();
            _injector.InjectInto(consumer);

            Assert.AreSame(service, consumer.PrivateField);
            Assert.AreSame(service, consumer.PublicProperty);
        }

        [Test]
        public void InjectInto_ResolvesInheritedFields()
        {
            var service = new ServiceA();
            _injector.MapValue<ServiceA>(service);

            var consumer = new DerivedConsumer();
            _injector.InjectInto(consumer);

            Assert.AreSame(service, consumer.Inherited);
        }

        [Test]
        public void MapSingletonOf_BindsImplementationToInterface()
        {
            _injector.MapSingletonOf<IGreeter, EnglishGreeter>();

            Assert.IsInstanceOf<EnglishGreeter>(_injector.Get<IGreeter>());
            Assert.AreEqual("hello", _injector.Get<IGreeter>().Greet());
        }

        [Test]
        public void MapOrGetSingleton_IsIdempotent()
        {
            var first = _injector.MapOrGetSingleton<ServiceB>();
            var second = _injector.MapOrGetSingleton<ServiceB>();

            Assert.AreSame(first, second);
        }

        [Test]
        public void InjectInto_SkipsObjectAlreadyInjectedInto()
        {
            var first = new ServiceA();
            _injector.MapValue<ServiceA>(first);

            var consumer = new TrackedConsumer();
            _injector.InjectInto(consumer);
            Assert.AreSame(first, consumer.Service);

            // Re-register and inject again: the guard must keep the first resolution.
            _injector.Unmap<ServiceA>();
            _injector.MapValue<ServiceA>(new ServiceA());
            _injector.InjectInto(consumer);

            Assert.AreSame(first, consumer.Service, "Injected flag did not prevent a second pass.");
        }

        [Test]
        public void TryMapManager_InitializesOnFirstRegistrationOnly()
        {
            _injector.MapValue<ServiceA>(new ServiceA());

            var manager = new CountingManager();
            var returned = _injector.TryMapManager<CountingManager>(manager);

            Assert.AreSame(manager, returned);
            Assert.AreEqual(1, manager.InitializeCalls);
            Assert.IsNotNull(manager.Service, "Initialize() ran before injection completed.");

            // A second registration must return the live instance and not re-initialise.
            var duplicate = new CountingManager();
            var afterDuplicate = _injector.TryMapManager<CountingManager>(duplicate);

            Assert.AreSame(manager, afterDuplicate);
            Assert.AreEqual(1, manager.InitializeCalls);
            Assert.AreEqual(0, duplicate.InitializeCalls);
        }

        [Test]
        public void MissingMapping_LogsAndLeavesMemberUntouched()
        {
            // Consumer declares two injected members, so two errors are expected.
            LogAssert.Expect(LogType.Error, new Regex("Missing injection rule for ServiceA"));
            LogAssert.Expect(LogType.Error, new Regex("Missing injection rule for ServiceA"));

            var consumer = new Consumer();
            _injector.InjectInto(consumer);

            Assert.IsNull(consumer.PrivateField);
        }

        [Test]
        public void MissingMapping_CanBeSuppressed()
        {
            var consumer = new Consumer();

            // No LogAssert.Expect here - suppressErrors must keep the log clean.
            _injector.InjectInto(consumer, suppressErrors: true);

            Assert.IsNull(consumer.PrivateField);
        }

        [Test]
        public void Unmap_RemovesRegistration()
        {
            _injector.MapValue<ServiceA>(new ServiceA());
            Assert.IsNotNull(_injector.Get<ServiceA>());

            _injector.Unmap<ServiceA>();

            Assert.IsNull(_injector.Get<ServiceA>());
        }
    }
}
