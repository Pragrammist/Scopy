--

# Scopy Project: The Concept of Scope-Based Programming in .NET

**Scopy** is a proposal for the evolution of C# and .NET through the introduction of a primary abstraction: the **Scope**. This is not just a tool, but a fundamental way to manage data, memory, and program behavior.

## 1. Scope as a New Entity (Contextual Ownership)

The core idea is to make the scope a physical boundary for data.

```csharp
[Scope]
public static UserData GetUserProfile([Scope] UserId id)
{
    // Profile retrieval logic
}

```

**Under the hood (code generation):**

```csharp
public static UserData GetUserProfileScoped()
{
    // Resolving value from the current context
    var id = CurrentScope.Resolve<UserId>();
    
    // Creating a nested scope
    CurrentScope.Push(name: "UserProfileScope");
    
    // Calling the original method
    var result = GetUserProfile(id);
    
    // Closing the scope and providing the result to the calling context
    CurrentScope.Pop();
    CurrentScope.Provide(result);
    
    return result;
}

```

**Key Principle:** All data within a Scope is **immutable**. This ensures predictability and allows for efficient memory management in the future. Furthermore, within a single Scope, you cannot provide (`Provide`) more than one instance of the same type.

## 2. Automatic await Generation (Clean Code)

To eliminate redundant `Task` and `ValueTask` boilerplate, Scopy proposes generating a `GetAwaiter()` method for types marked with the attribute.

```csharp
[Scope]
public record struct OrderInfo(int Id, string Status);

// This allows writing:
var order = await GetOrderById(id); 
// Instead of ValueTask<OrderInfo> or Task<OrderInfo>

```

This solves the problem of "polluting" method signatures with asynchronous wrappers.

## 3. Contract Functions (Polymorphism via Scope)

Scopes allow switching logic implementations based on context without interfaces or DI containers. Calls are always made through an automatically generated wrapper method.

**Contract Declaration** (library or interface layer):

```csharp
[Scope]
public partial static PaymentResult ProcessPayment();

```

**Contract Implementation** (in another module):

```csharp
[Scope]
public static PaymentResult ProcessStripePaymentImpl() 
{
    // Stripe implementation
    return new(...);
}

```

**Usage** (The generator links the call and implementation):

```csharp
[Scope]
public static void ExecuteBusinessLogic()
{
    // The user simply calls the Scoped method.
    // The GENERATOR will produce code that performs CurrentScope.Provide(ProcessStripePaymentImpl)
    // and invokes the original method within a new Scope.
    var result = ProcessPaymentScoped(); 
}

```

## 4. Named Scopes and Contractual Implementations

Names allow for switching function implementations and demarcating code access. Analyzers ensure that a call is only possible if the name attributes match.

```csharp
[Scope]
public partial static Data GetInfo();

// Implementation for a specific DB context
[Scope]
[DatabaseContext]
public static Data GetInfoFromDbImpl() => new(...);

// Default implementation
[Scope]
public static Data GetInfoDefaultImpl() => new(...);

// --- Usage and Demarcation ---

[Scoped]
[DatabaseContext]
public static void ProcessWithDb()
{
    // GetInfoFromDbImpl is called due to the DatabaseContext match
    var data = GetInfoScoped(); 
}

[Scoped]
[UIContext]
[DatabaseContext]
public static void SyncUiWithDb()
{
    // Here, you CAN call both UI and Database methods because both attributes are present.
    // This serves as a legal "bridge" between layers.
    var data = GetInfoScoped(); 
    UpdateInterface(data);
}

[Scoped]
public static void ProcessDefault()
{
    // GetInfoDefaultImpl (default implementation) is called
    var data = GetInfoScoped();
}

```

**Key Point:** Analyzers operate on a permission basis. If a method requires `[DatabaseContext]`, you must be inside a function marked with that attribute. However, you can stack as many attributes as needed (`[SomeName1]`, `[SomeName2]`), expanding the capabilities of the current method and allowing it to interact with different subsystems simultaneously.

This makes dependency management visual: you can immediately see from the function header which "worlds" it has access to.

## 5. Analyzers and Internal Safety (API)

Strict rules are introduced to maintain system stability:

* **Duplicate Prohibition:** The analyzer forbids `Provide` for a type that already exists in the current Scope (a child scope must be created).
* **API Isolation:** Access to `CurrentScope` is allowed only via the `[AllowUseCurrentScopeInternalApi]` attribute. This is reserved for writing adapters and code generation.
* **World Separation:** The system works only with **static methods and record structs**. This separates the mutable OOP world from the predictable Scope world.

---

## 6. Paged Memory Model (No GC)

This is a "future-proofing" feature designed to achieve C++/Rust level performance within C#. Each Scope is allocated paged memory (similar to 4KB OS pages).

**Mechanism:**

1. **Object Creation:** A generated `CreateSomeData()` method is called, requesting memory directly (via `malloc` or similar), returning a `ref var`.
2. **Incrementality:** When an object is "changed," a new entry is created in memory that references the old data, adding only the changes.
3. **Destruction:** When the Scope ends, **the entire memory page is freed instantly**. There is no need for a Garbage Collector (GC).
4. **Data Transfer:** If an object is returned to a parent Scope, a fast data copy to the parent's memory is performed before the child page is destroyed.

## 7. Asynchrony and the Actor Model

A Scope effectively becomes an **Actor**—it has its own stack and isolated memory.

* **Parallelism:** `async SomeMethod()` creates a parallel Scope. Due to immutability, data does not need to be copied for reading—a `Copy-on-Write` principle is used (without the actual copy, as mutations are forbidden).
* **Message Passing:** The function's return value is the message.
* **Data Streams:** Implemented via a model similar to `IAsyncEnumerable`, where the child Scope generates data and the parent receives copies via `yield return`.

## Conclusion: Paradigm Convergence

Introducing the **Scope** abstraction blurs the lines between programming styles:

* **FP:** Immutability and pure functions.
* **OOP:** Syntactic linking of data and functions via extensions and `ref var`, but without the danger of mutations.
* **Procedural:** Clear execution sequence and resource ownership.

**Scopy** is the path to high-level C# that runs at native speeds, manages resources automatically, and eliminates entire classes of design errors.

---

# Scopy Memory Model: Incremental Paged Stack (No GC)

In traditional languages (C#, Java), memory is chaos where the Garbage Collector (GC) constantly hunts for what can be deleted. In **Scopy**, memory is a strict hierarchy of "plates" (pages) tied to the lifecycle of functions (Scopes).

## 1. Paged Structure (Page Table)

Instead of asking the system for memory for every individual object, each Scope requests one or more **pages** (usually 4KB, like in Linux) upon creation.

* **How it works:** If you create a 100-byte object, the Scope allocates space for it in its current page. If the page is full, the Scope requests the next page from the OS.
* **Why it’s fast:** This eliminates fragmentation. Memory allocation is just an increment (addition) of a pointer. This is as fast as the system stack.

## 2. Incrementality and Data "Mutation"

The golden rule: **data is immutable**. You cannot go into memory and change a field value of an existing object.

* **The Problem:** How do we "change" state then?
* **The Solution:** If you want to change an object property, Scopy creates a **new object** in the same memory page. This new object contains the updated value and simply holds references to (or copies) the rest of the old object's data into the same page.
* **The Result:** Memory within a Scope always grows "upward" (incrementally). We never waste time looking for free space in the middle of a page or overwriting old data.

## 3. Instant Destruction Mechanism

This is the primary replacement for the GC.

* **Algorithm:** When a function (Scope) finishes its work, **all memory** allocated for that Scope (all its pages) is destroyed at once.
* **Efficiency:** We don't need to traverse a reference tree and check every object. We simply hand the memory pages back to the OS with a single command. This happens in constant time (), regardless of whether there was one object or a million.

## 4. Data Transfer: Deep Copy on Exit

Since the child Scope memory is about to disappear and we need the function result in the parent Scope, a copying process occurs.

* **Mechanics:** Before destroying the child Scope pages, the function result "migrates" (is copied) to the parent Scope's page.
* **Speed:** Because memory is paged and data is packed tightly (linearly), copying is extremely fast (at `memcpy` levels).
* **Safety:** The Parent Scope receives a clean copy. All temporary data used by the Child Scope for intermediate calculations is instantly burned along with its pages.

## 5. Hierarchy of "Stacks"

Imagine this as a stack of transparent sheets:

1. **Root Scope:** Lives forever (as long as the program runs). Creating everything here leads to OutOfMemory.
2. **Request Scope:** Lives while a request is being processed. Create object -> work -> request closes -> memory is instantly free.
3. **Local Scope:** Nested loop or function. Lives for microseconds.

## Why is this better than standard GC?

1. **No Pauses (Stop-the-world):** The program never freezes for "cleanup."
2. **Cache Locality:** Since data in a page lies right next to each other, the CPU reads it lightning-fast.
3. **Predictability:** You always know how much memory your algorithm consumes just by looking at the Scope nesting.

---

# Syntax and Paradigm: Native Scope in C#

The library implementation via `ref var` and `CreateSomeData` is a way to survive in current realities. But the ideal vision implies that the C# compiler itself understands the Scope concept and adapts standard keywords to it.

## 1. Native Records Support without GC

In the Scopy model, a `record` becomes more than just "class with sugar"—it becomes a deterministic data structure.

* **Mutation and Logic Prohibition:** Inside Scope-oriented records, standard properties with `set`, variables, and methods are forbidden. Only data is allowed. Logic is moved to static functions (extensions).
* **Creation Syntax:** We move away from `new`. Instead, a direct type call or special syntactic sugar is used, which allocates memory in the current Scope page.

```csharp
[ScopeRecord]
public record User(int Id, string Name);

// Usage
var user = User(1, "Alex"); // Compiler knows memory is taken from the current Scope

```

## 2. True Immutability via `with` and `.`

The current C# syntax is perfectly suited for incremental memory if its "under-the-hood" meaning is changed.

* **The `with` Construct:** When you write `user with { Name = "Max" }`, the compiler doesn't look for space in the heap. It simply appends the changed field to the current Scope page and creates a new reference.
* **Assignment Syntax (`a.Name = ""`):** To avoid breaking OOP habits, this syntax can be allowed but made functional. When you write `user.Name = "Ivan"`, you aren't changing the old object. The compiler implicitly does `user = user with { Name = "Ivan" }`.
* **Note:** This creates a new object in the current page. The old one remains untouched, guaranteeing predictability for other functions using the same reference.

## 3. Inheritance and Interfaces (Contextual Adaptation)

Classic inheritance in OOP is memory hell. In Scopy, it is replaced by composition of data and contracts.

* **Interfaces as Function Contracts:** Instead of a virtual method table (vtable), an interface in Scopy is a set of **Contract Functions**. Implementation is pulled from the Scope, not the object.
* **Data Inheritance:** This is simply nesting one structure inside another.

```csharp
public record Admin(User BaseUser, int Level); 
// Dot access: admin.Name -> translates to admin.BaseUser.Name

```

## 4. "File as Function" Concept (Scripting Experience)

Blurs the line between module and logic.

```csharp
// File: ProcessOrder.cs
[Scope] arg Order order;
[Scope] [Database] arg IRepository repo;

// The entire file code is the function body.
if (order.Amount > 100) 
{
    repo.Save(order with { Discount = 0.1 });
}

```

## 5. Hierarchy: "Meaning Over Keywords"

We move away from FP vs OOP debates.

* **OOP-style:** We use `obj.DoSomething()` because it's convenient for IDE autocompletion. But under the hood, it's a static function call where the first argument is data from the Scope.
* **FP-style:** We use pure transformations because the Scopy memory model makes them cheaper than mutations.

---

# Scopy Analyzer System: Compile-time Safety Guarantee

In a world without GC, memory or context management errors can be fatal. Thus, Scopy analyzers are a mandatory part of the build.

## 1. Escape Analysis

The most critical analyzer for the paged memory model. It ensures data does not outlive its Scope.

* **Rule:** It is forbidden to assign a reference from a `ChildScope` to a variable or field belonging to a `ParentScope`.
* **Goal:** Eliminate "dangling pointers" where a Scope is destroyed but something outside tries to read its memory.

## 2. Named Context Lock (Access Control)

Ensures physical isolation of architectural layers.

* **Rule:** A method marked `[DatabaseContext]` can only be called inside another method with the same attribute.
* **Goal:** Nip spaghetti code in the bud. You literally cannot call DB save logic from UI rendering; the compiler will flag it.

## 3. Uniqueness and Provide Purity (DI-Safety)

Replaces heavy Runtime DI checks with instant compile-time verification.

* **Rule 1:** Forbidden to `Provide<T>` for a type already in the current Scope. To replace an object, create a child Scope (`Push`).
* **Rule 2:** Forbidden to `Resolve<T>` if no one in the Scope chain has provided that type or contract.

## 4. Mutation and OOP-Mixing Control

Acts as a "purity filter."

* **Rule:** Inside `[Scope]` methods, the use of `class`, `new` (for standard objects), and access to mutable global variables is forbidden.
* **Goal:** Separate the mutable "mess" of the old OOP world from the clean paged memory model.

---

# Asynchrony and Actor Model 2.0: Fearless Parallelism

In standard C#, asynchrony creates "state machine hell" and contention for shared resources. In Scopy, every async call is the birth of a new isolated world (Scope-Actor).

## 1. Scope as a Unit of Parallelism

When you call `async SomeMethod()`, the runtime creates a parallel Scope.

* **Own Stack, Own Memory:** The method gets its own memory page (Arena). It cannot "climb" into the parent's page and corrupt it because everything is immutable.
* **Zero-copy Reads:** Since parent data is immutable, the child Scope can safely read it. We don't need expensive deep copying at the start of a task. We just give the child a pointer: "Read here, it will never change."

## 2. Actor Nature (Isolation)

* **Actor = Scope:** Each async Scope acts as an actor, owning its data and executing logic in isolation.
* **Result = Message:** In actor models, you "send a message." In Scopy, you just return a result.
* **Zero-copy Transfer:** When a method ends, its result is a "parcel" that flies into the parent Scope via fast page-to-page copying before the child page is wiped.

## 3. Iron Rule of Multi-threading (Multi-Core by Design)

* **Root Parallelism:** 4 async tasks from the Root Scope are guaranteed to be spread across physical cores (Threads).
* **Child Determinism:** If a task already occupying a core starts 10 more `async` calls, they do not take new threads. They run cooperatively on the same core to prevent context-switching avalanches.

## 4. Collapsing Async/Await (No More State Machines)

Because each Scope has its own "parallel stack" (paged memory), we don't need to turn every function into a complex state-machine class.

* **Yield/Resume at Memory Level:** An `await` is just saving the current pointer in the Scope page. When data is ready, the CPU simply returns to that pointer. This is magnitudes lighter than a `ValueTask`.

## 5. Message Passing via Streams (Yield Return)

For data streams, a producer Child Scope generates an object, copies it to the parent's buffer, and fires a signal. The parent wakes up and processes the copy while the producer prepares the next one.

**Summary:** Asynchrony in Scopy provides the power of Erlang with the syntax and speed of C#.

---

Would you like me to generate a summary table comparing **Standard .NET** vs **Scopy** for a quick presentation, or perhaps a more detailed technical specification for one of the Analyzers?
