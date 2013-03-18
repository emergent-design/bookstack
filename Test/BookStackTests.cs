using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using NUnit.Framework;

using BookSleeve;
using BookStack;
using ServiceStack.Text;


namespace BookStack.Tests
{
	[TestFixture]
	public class BookStackTests
	{
		internal static RedisConnection GetConnection()
		{
			var result = new RedisConnection("127.0.0.1");

			result.Error += (s, e) => Trace.WriteLine(e.Exception.Message, e.Cause);
			result.Wait(result.Open());

			return result;
		}


		[TestFixtureSetUp]
		public void Initialise()
		{
			using (var db = GetConnection())
			{
				var ids = db.Wait(db.Keys.Find(0, "urn:entity:*"));

				if (ids.Length > 0) db.Wait(db.Keys.Remove(0, ids));

				db.WaitAll(
					db.Keys.Remove(0, "id:entity"),
					db.Keys.Remove(0, "ids:entity")
				);
			}
		}


		[Test]
		public void IncrementalId()
		{
			using (var db = GetConnection())
			{
				var first	= db.NextId<Entity>(0);
				var second	= db.NextId<Entity>(0);

				Assert.AreNotEqual(first, second);
				Assert.IsTrue(first == second - 1);
			}
		}


		[Test]
		public void Store()
		{
			using (var db = GetConnection())
			{
				var entity = new Entity { 
					Id		= db.NextId<Entity>(0), 
					Name	= "One", 
					Data	= new Dictionary<string, int> { { "a", 0 }, { "b", 1 } },
					Binary	= new byte [] { 0x00, 0xf0, 0x88, 0x12 }
				};

				db.Wait(db.Store(0, entity));
				var serialised = db.Wait(db.Strings.GetString(0, string.Format("urn:entity:{0}", entity.Id)));

				Assert.AreEqual(entity.ToJson(), serialised);
			}
		}


		[Test]
		public void StoreAll()
		{
			using (var db = GetConnection())
			{
				var entities = new Entity [] {
					new Entity { Id = db.NextId<Entity>(0), Name = "One" },
					new Entity { Id = db.NextId<Entity>(0), Name = "Two" },
				};

				db.Wait(db.StoreAll(0, entities));

				Assert.AreEqual(entities[1].Name, db.Wait(db.Get<Entity>(0, entities[1].Id)).Name);
			}
		}


		[Test]
		public void Get()
		{
			using (var db = GetConnection())
			{
				var entity = new Entity {
					Id		= db.NextId<Entity>(0),
					Name	= "One",
					Data	= new Dictionary<string, int> { { "a", 0 }, { "b", 1 } },
					Binary	= new byte [] { 0x00, 0xf0, 0x88, 0x12 }
				};

				db.Wait(db.Store(0, entity));

				var stored = db.Wait(db.Get<Entity>(0, entity.Id));

				Assert.IsNull(db.Wait(db.Get<Entity>(0, 1234)));
				Assert.AreEqual(entity.Id, stored.Id);
				Assert.AreEqual(entity.Name, stored.Name);
				CollectionAssert.AreEquivalent(entity.Data, stored.Data);
				CollectionAssert.AreEquivalent(entity.Binary, stored.Binary);
			}
		}


		[Test]
		public void GetAll()
		{
			using (var db = GetConnection())
			{
				var entities = new Entity [] {
					new Entity { Id = db.NextId<Entity>(0), Name = "One" },
					new Entity { Id = db.NextId<Entity>(0), Name = "Two" },
				};

				db.Wait(db.StoreAll(0, entities));

				var stored = db.Wait(db.GetAll<Entity>(0));

				Assert.That(stored.Any(e => e.Id == entities[1].Id));
			}
		}


		[Test]
		public void Delete()
		{
			using (var db = GetConnection())
			{
				var entity = new Entity { Id = db.NextId<Entity>(0), Name = "One" };

				db.Wait(db.Store(0, entity));

				Assert.That(db.Wait(db.Delete<Entity>(0, entity.Id)));
				Assert.IsFalse(db.Wait(db.Delete<Entity>(0, entity.Id)));
			}
		}


		[Test]
		public void DeleteAll()
		{
			using (var db = GetConnection())
			{
				var entities = new Entity [] {
					new Entity { Id = db.NextId<Entity>(0), Name = "One" },
					new Entity { Id = db.NextId<Entity>(0), Name = "Two" },
				};

				db.Wait(db.StoreAll(0, entities));

				Assert.That(db.Wait(db.DeleteAll<Entity>(0)) > 0);
				Assert.AreEqual(0, db.Wait(db.DeleteAll<Entity>(0)));
				Assert.AreEqual(0, db.Wait(db.GetAll<Entity>(0)).Count());
			}
		}
	}

}