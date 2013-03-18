using System;
using System.Collections.Generic;


namespace BookStack.Tests
{
	class Entity
	{
		public long Id		{ get; set; }
		public string Name	{ get; set; }
		
		public Dictionary<string, int> Data { get; set; }
		public byte [] Binary { get; set; }
	}
}
