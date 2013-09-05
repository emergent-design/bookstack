// Copyright (c) 2013, Emergent Design Ltd. All rights reserved. Use of this source code
// is governed by a BSD-style licence that can be found in the LICENCE file.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

using BookSleeve;
using ServiceStack.Text;


namespace BookStack
{
	public static class BookStackExtensions
	{
		/// <summary>
		///   Generates an incremental ID for a given type. Each type is assigned their own incrementor in redis using the key "id:typename". 
		///   This is the only synchronous method in BookStack since it is assumed that a new ID is required for immediate assignment.
		/// </summary>
		/// <returns> the new ID</returns>
		public static long NextId<T>(this RedisConnection redis, int db)
		{
			return redis.Wait(redis.Strings.Increment(db, string.Format("id:{0}", typeof(T).Name.ToLower())));
		}


		/// <summary>
		///   Stores the given entity to redis. It is assumed that the entity has a public property named "Id" which is used to form
		///   the redis key. An exception will occur if an ID property does not exist.
		///   The object will be stored under the key "urn:typename:id" and if the key already exists it will be overwritten with the new
		///   serialized object instance.
		///   The ID of the object is also added to a set under the key "ids:typename" and is used for the fast retrieval of all entities of 
		///   a given type.
		/// </summary>
		/// <returns> the same entity instance that was passed in</returns>
		public static Task<T> Store<T>(this RedisConnection redis, int db, T entity)
		{
			object id = entity.GetType().GetProperty("Id").GetGetMethod().Invoke(entity, new object[0]);

			return redis.Store(db, id, entity);
		}


		/// <summary>
		///   Stores the given entity to redis. The ID must be specified as this is used to form the redis key. The object will be stored 
		///   under the key "urn:typename:id" and if the key already exists it will be overwritten with the new serialized object instance.
		///   The ID of the object is also added to a set under the key "ids:typename" and is used for the fast retrieval of all entities of 
		///   a given type.
		/// </summary>
		/// <returns> the same entity instance that was passed in</returns>
		public static Task<T> Store<T>(this RedisConnection redis, int db, object id, T entity)
		{
			string urn	= string.Format("urn:{0}:{1}", typeof(T).Name.ToLower(), id);
			var tasks	= new Task [] {
				redis.Strings.Set(db, urn, entity.ToJson()),
				redis.Sets.Add(db, string.Format("ids:{0}", typeof(T).Name.ToLower()), id.ToString())
			};

			return Task.Factory.ContinueWhenAll(tasks, t => entity);
		}


		/// <summary>
		///   Stores a collection of entities to redis. Each item must have a public property named "Id" which is used to form the redis keys, 
		///   otherwise an exception will occur. Each entity is stored in the same way as the Store method (except that this is performed as a 
		///   batch process).
		/// </summary>
		/// <returns> the number of new entries only, existing entries will simply be updated</returns>
		public static Task<long> StoreAll<T>(this RedisConnection redis, int db, IEnumerable<T> entities)
		{
			return redis.StoreAll(db, entities.ToDictionary(e => e.GetType().GetProperty("Id").GetGetMethod().Invoke(e, new object[0]), e => e));
		}


		/// <summary>
		///   Stores a collection of entities to redis. The items are expected as a dictionary where each key is the ID for the corresponding
		///   entity instance. Each entity is stored in the same way as the Store method (except that this is performed as a batch process).
		/// </summary>
		/// <returns> the number of new entries only, existing entries will simply be updated</returns>
		public static Task<long> StoreAll<T>(this RedisConnection redis, int db, Dictionary<object, T> entities)
		{
			string urn	= string.Format("urn:{0}", typeof(T).Name.ToLower());
			var tasks	= new Task [] { 
				redis.Strings.Set(db, entities.ToDictionary(i => string.Format("{0}:{1}", urn, i.Key), i => i.Value.ToJson())),
				redis.Sets.Add(db, string.Format("ids:{0}", typeof(T).Name.ToLower()), entities.Keys.Select(k => k.ToString()).ToArray())
			};

			return Task.Factory.ContinueWhenAll(tasks, t => (tasks[1] as Task<long>).Result); 
		}


		/// <summary>
		///   Retrieves an entity from redis by ID. If the ID does not exist then null is returned.
		/// </summary>
		/// <returns> the deserialized entity instance or null if the ID cannot be found</returns>
		public static Task<T> Get<T>(this RedisConnection redis, int db, object id)
		{
			string urn	= string.Format("urn:{0}:{1}", typeof(T).Name.ToLower(), id);
			var task	= redis.Strings.GetString(db, urn);

			return task.ContinueWith(t => JsonSerializer.DeserializeFromString<T>(t.Result));
		}


		/// <summary>
		///   Gets all entities of the given type from redis. The returned collection will be empty
		///   if no entities of that type exist.
		/// </summary>
		/// <returns> a collection of entities</returns>
		public static Task<IEnumerable<T>> GetAll<T>(this RedisConnection redis, int db)
		{
			string urn	= string.Format("urn:{0}", typeof(T).Name.ToLower());
			var task	= redis.Sets.GetAllString(db, string.Format("ids:{0}", typeof(T).Name.ToLower()));

			return task.ContinueWith(ids => {
				var items = ids.Result.Length > 0 
					? redis.Wait(redis.Strings.GetString(db, ids.Result.Select(i => string.Format("{0}:{1}", urn, i)).ToArray())) 
					: new string[0];

				return items.Select(i => JsonSerializer.DeserializeFromString<T>(i));
			});
		}


		/// <summary>
		///   Deletes the specified entity from redis. 
		/// </summary>
		/// <returns> true if successful or false if the entity cannot be found</returns>
		/// <param name="redis">Redis.</param>
		/// <param name="db">Db.</param>
		/// <param name="id">Identifier.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static Task<bool> Delete<T>(this RedisConnection redis, int db, object id)
		{
			string urn	= string.Format("urn:{0}:{1}", typeof(T).Name.ToLower(), id);
			var tasks	= new Task<bool> [] {
				redis.Keys.Remove(db, urn),
				redis.Sets.Remove(db, string.Format("ids:{0}", typeof(T).Name.ToLower()), id.ToString())
			};

			return Task.Factory.ContinueWhenAll(tasks, t => tasks[0].Result && tasks[1].Result);
		}


		/// <summary>
		///   Deletes all entities of the given type from redis. 
		/// </summary>
		/// <returns> the number of entities that were deleted</returns>
		public static Task<long> DeleteAll<T>(this RedisConnection redis, int db)
		{
			var task = redis.Sets.GetAllString(db, string.Format("ids:{0}", typeof(T).Name.ToLower()));

			return task.ContinueWith(ids => redis.Wait(redis.DeleteAll<T>(db, task.Result)));
		}


		/// <summary>
		///   Deletes a number of entities from redis at once.
		/// </summary>
		/// <returns> the number of entities that were deleted</returns>
		public static Task<long> DeleteAll<T>(this RedisConnection redis, int db, string [] ids)
		{
			string urn = string.Format("urn:{0}", typeof(T).Name.ToLower());

			if (ids.Count() > 0)
			{
				var tasks = new Task [] {
					redis.Keys.Remove(db, ids.Select(i => string.Format("{0}:{1}", urn, i)).ToArray()),
					redis.Sets.Remove(db, string.Format("ids:{0}", typeof(T).Name.ToLower()), ids.Select(i => i.ToString()).ToArray())
				};

				return Task.Factory.ContinueWhenAll(tasks, t => (tasks[1] as Task<long>).Result);
			}

			// This bit can be replaced with Task.FromResult when using .NET 4.5
			var result = new TaskCompletionSource<long>();
			result.SetResult(0);

			return result.Task;
		}
	}
}
