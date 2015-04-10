using System.ComponentModel.DataAnnotations;

namespace Akka.Persistence.EntityFramework.Models
{
	internal class Message
	{
		[Key]
		public long Id { get; set; }
		public string PersistentId { get; set; }
		public long SequenceNr { get; set; }
		public bool Deleted { get; set; }
		public byte[] Payload { get; set; }
	}
}
