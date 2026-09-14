# Unity Systems

Two systems from a commercial Unity mobile game, published here as code samples: a small
inversion-of-control container with a scene-bootstrap layer, and a virtualised list built on
top of it.

They are in one repository on purpose — the list is a real consumer of the container, which
shows the architecture doing something rather than describing itself.

---

## 1. Dependency injection

A small inversion-of-control container and scene-bootstrap layer.

Dependencies are declared with a single attribute and resolved by type. It works on
`MonoBehaviour`s and on plain C# classes alike, survives repeated scene loads, and adds no
third-party dependency to the build.

```csharp
public class AudioManager : MonoBehaviour, IManager
{
    [Inject] private Settings _settings;   // that is the whole wiring

    public void Initialize()               // runs after injection, not in Awake
    {
        _volume = _settings.MasterVolume;
    }
}
```

---

## Why this exists

Unity gives you no native way to hand a dependency to a script. The three usual answers all
break down once a project gets large:

| Approach | Where it fails |
|---|---|
| Serialized inspector reference | Does not survive a prefab instantiated at runtime; disappears silently during a refactor |
| Static singleton | Hard-wires a class to one implementation; cannot be tested or swapped |
| `FindObjectOfType()` | Slow, and breaks whenever scene loading order changes |

This container was written to satisfy four constraints: one attribute to declare a dependency,
works for `MonoBehaviour` and plain C# classes, survives repeated scene loads, and stays small
enough to own outright. It is roughly 250 lines.

## How it works

1. **Registration** — a composition root registers every manager and service under its type.
2. **Declaration** — any class marks a field or property with `[Inject]`. It knows nothing
   about the source or the mechanism.
3. **Injection** — `Injector.InjectInto(object)` reflects over the marked members and writes
   the registered instances into them.
4. **Bootstrap** — on scene load, `SceneBootstrap` builds the controller tree, injects into the
   root controllers and initialises them. Every `ControllerContainer` repeats those two steps
   for its own children, so the pass walks the tree top down and a parent is always ready
   before its children.

## Design notes

**Field injection, not constructor injection.** A `MonoBehaviour` cannot have a constructor, so
constructor injection as the .NET world knows it is unavailable. Resolution happens on an
existing instance instead, over fields and properties — including private and inherited ones.

**A guard against repeated reflection.** Objects implementing `IInjectable` carry an `Injected`
flag, and a second injection attempt is skipped. This lets `InjectInto()` be called from
anywhere without the caller tracking who injected what.

**Duplicate managers on scene reload.** `TryMapManager()` handles the case where a manager is a
component in a scene and that scene is loaded again: the freshly loaded duplicate is destroyed
before it can register listeners or start coroutines, and the caller gets back the instance that
is already live, so existing references stay valid. This was the original reason the container
was written.

**Failures are loud.** A missing registration does not throw — the member stays null and the
log names the missing type, the class that asked for it, and every type that *was* registered at
that moment. That last part removes most of the guesswork.

## Usage

Register everything in one place — a composition root that runs before anything else and
survives scene changes:

```csharp
[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
    [SerializeField] private Settings _settings;
    [SerializeField] private AudioManager _audioManager;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        Injector injector = Injector.Instance;

        injector.MapAndInjectInto(injector);            // the container itself
        injector.MapValue<Settings>(_settings);         // a value it does not own
        _audioManager = injector.TryMapManager(_audioManager);  // a scene manager
        injector.MapOrGetSingleton<SaveService>();      // a plain C# service
        injector.MapSingletonOf<IAnalytics, ConsoleAnalytics>(); // interface binding
    }
}
```

Then give each scene a bootstrap:

```csharp
public class MainMenuBootstrap : SceneBootstrap
{
    [Inject] private SceneManager _sceneManager;
}
```

Scene controllers derive from `Controller` or `ControllerContainer` and are picked up
automatically. Objects created at runtime derive from `InjectableMonoBehaviour` and request
injection themselves in `Awake()`.

See `Samples/` for the full picture.

## Limitations

Worth knowing before you adopt it:

- **No lifetime scoping.** Everything is effectively a singleton. A scope bound to a scene or a
  session would remove the manual `Unmap()`/`MapValue()` at scene transitions.
- **Reflection is not cached.** `InjectInto()` reflects on every call. The `Injected` flag
  covers long-lived objects, but short-lived instances — list items, pooled objects — pay the
  cost repeatedly. A `Type → FieldInfo[]` cache would fix it without changing the public API.
- **`Injector.Instance` is a service locator.** It exists for the bootstrap entry points that
  cannot be injected into. Everything else should receive its dependencies.
- **No circular dependency detection.** Two services that inject each other will resolve, but
  whichever is registered first sees a null until the second registration completes.

## Why not Zenject or VContainer

Partly timing — when the first version of this was written, those were not the obvious choice
they are today.

It has stayed for a more practical reason. A container you own can be extended into places a
framework cannot reach without forking it. The case I keep running into is networking: a
networked object is instantiated by the networking layer, not by the container, so the container
never sees it and never injects into it. Neither Zenject nor VContainer ships an integration for
that, and the common workaround is a static container plus a manual resolve call in every spawned
behaviour — a service locator wearing a DI badge. With 250 lines you can hook injection into the
spawn path itself and keep the declarative form.

For a new project with no such constraint, VContainer is the sensible default and this is not
trying to compete with it.

---

## 2. Virtualised list

`DynamicListScroller` keeps a constant number of row instances no matter how large the data set
is — ten thousand rows cost the same number of GameObjects as ten — and, unlike most recycling
lists, it supports **rows of different heights**, including rows that resize at runtime.

```csharp
// The whole setup: a row controller and its data type.
public class FriendList : DynamicListScroller<FriendRow, FriendData> { }

_friendList.SetData(friends);
```

### How the recycling works

The pool holds just enough rows to cover the viewport plus a margin, sized at initialisation
from the viewport height and the prefab. As the content scrolls, the row that leaves the top is
moved to the bottom and refilled with the next data entry, so a data index maps onto a pool
index with a positive modulo — the pool behaves as a ring buffer in both directions. Nothing is
instantiated or destroyed while scrolling.

The two recycling thresholds are deliberately far apart. The distance at which a row is moved
down has to be more than one row height from the distance at which it is moved back up,
otherwise a row sitting exactly on the boundary would bounce between the two every frame.

### Variable heights

Every row's measured height is kept in a cache, and positions are derived from that cache rather
than from a fixed row height. A row can change its height two ways:

| From | When | What happens |
|---|---|---|
| `Refresh()` | while the row is being filled with data | the list measures the row right after Refresh and updates the cache — no second layout pass |
| `ExpandItem(height)` | later, in response to a tap | the row calls back into the list through `IListController`, the cache is updated and the layout re-runs |

Call `ExpandItem` from a coroutine to expand gradually.

Because rows are recycled, per-row state has to live in the data, not in the controller — the
row that displayed an entry a moment ago may already be showing a different one. `Samples/`
shows the pattern.

### Limitations

- **Vertical only.** A horizontal axis was never needed and is not implemented.
- **Pivots.** The content and viewport pivots are assumed to be at the top `(0, 1)`. The list
  forces the pivot and anchors of every row it adopts.
- **`GetItemPosition` is linear in the index**, so a full layout pass is O(n²) in the data
  count. Fine at the sizes this was built for; a prefix-sum array would make it constant time.
- **Scroll offsets are approximate for rows never rendered.** Heights that were measured are
  used as they are, the rest are approximated with the average of what is known — exact for a
  uniform list, close enough to land in the right place for a ragged one.

---

## What is in the box

```
Runtime/
  Injector.cs                  the container
  InjectAttribute.cs           the marker
  IInjectable.cs               already-injected flag
  IManager.cs                  initialisation hook for registered services
  InjectableMonoBehaviour.cs   base for objects created at runtime
  Controller.cs                base for scene objects in the bootstrap
  ControllerContainer.cs       a controller that owns other controllers
  ControllerUtils.cs           builds the controller tree of a scene
  ControllerFactory.cs         creates, injects and initialises controllers at runtime
  SceneBootstrap.cs            per-scene entry point

  Lists/
    ListController.cs          list that spawns, injects and owns its rows
    ListItemController.cs      base for one row
    DynamicListScroller.cs     recycling and variable-height layout
    DynamicScrollRect.cs       ScrollRect that exposes drag and scroll as callbacks
    IListController.cs         the non-generic face a row talks to
    IListItemController.cs     row contract
    ILayout.cs                 optional layout strategy
    DoubleClickButton.cs       optional second action on a row
    DynamicScrollerSettings.cs shared spacing configuration

Samples/                       composition root, services, controllers, a friends list
Tests/                         edit-mode tests for the container
```

## Installation

Copy `Runtime/` into your project, or add the repository as a Unity package via
**Package Manager → Add package from git URL**.

**Dependencies.** The container and the controller layer have none beyond Unity.
The list layer uses [DOTween](http://dotween.demigiant.com/) for animated scrolling.
[Odin Inspector](https://odininspector.com/) is optional — its attributes are behind
`#if ODIN_INSPECTOR`, so the code compiles without it. The samples use TextMeshPro.

## A note on origin

This code comes from a commercial Unity project and is published as a sample, not as a copy of
that codebase. The container, the controller layer and the list are complete and working; the
game's own composition root, its services, analytics, networking and game logic are not
included. Everything under `Samples/` was written for this repository. Namespaces have been
changed accordingly.

## License

MIT — see [LICENSE](LICENSE).
