using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Akka.Persistence.EntityFramework.Models
{
	internal class Message
	{
		[Key]
		public long Id { get; set; }
		[Index]
		public string PersistentId { get; set; }
		[Index]
		public long SequenceNr { get; set; }
		public bool Deleted { get; set; }
		public byte[] Payload { get; set; }
	}
}
