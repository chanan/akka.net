using Akka.Persistence.EntityFramework.Models;
using System.Data.Entity;

namespace Akka.Persistence.EntityFramework.Journal
{
	internal class JournalContext : DbContext
	{
		public DbSet<Message> Messages { get; set; }
	}
}
