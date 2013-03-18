BookStack
=========

BookStack is a small set of functions that extend the power of [BookSleeve](http://code.google.com/p/booksleeve)
with the object serialisation capabilities of [ServiceStack.Text](http://github.com/ServiceStack/ServiceStack.Text).

ServiceStack does actually contain a very nice [library](http://github.com/ServiceStack/ServiceStack.Redis) for 
accessing redis via a [typed client](http://github.com/ServiceStack/ServiceStack.Redis/wiki/IRedisTypedClient) but 
if you have an existing project that uses BookSleeve or wish to leverage its async and thread-safe API then BookStack can be used to provide simple typed access.


How it works
------------

When storing objects, BookStack follows a very similar process to ServiceStack.Redis (although there are slight
differences so they are not compatible with each other).

* Incremental IDs are stored at "id:typename".
* Lists of entity IDs are stored in sets at "ids:typename".
* The serialised entities themselves are stored at "urn:typename:id".

All typenames are converted to lower-case.


Installation
------------

You can either reference the tiny library or include the single source file within your solution. In either
case the extension functions become available via the standard RedisConnection.


Examples
--------

The examples assume an existing RedisConnection instance named "redis" and the entity model defined below.


### The example entity model

```csharp
class Entity
{
	public long Id		{ get; set; }
	public string Name	{ get; set; }
}
```


### Generate an ID

```csharp
var entity = new Entity { Id = redis.NextId<Entity>(0) };
```


### Store an entity

```csharp
var entity = new Entity { Id = redis.NextId<Entity>(0), Name = "BookStack" };

redis.Store(0, entity);
```


### Retrieve an entity

```csharp
var entity = redis.Wait(redis.Get<Entity>(0, id));
// or if using C# 5.0
var entity = await redis.Get<Entity>(0, id);
```


### Get all entities of a given type

```csharp
var entities = redis.Wait(redis.GetAll<Entity>(0));
```


### Delete an entity

```csharp
redis.Delete<Entity>(0, id);
```
