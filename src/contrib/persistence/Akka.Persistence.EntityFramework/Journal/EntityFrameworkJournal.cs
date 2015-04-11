using Akka.Actor;
using Akka.Persistence.EntityFramework.Models;
using Akka.Persistence.Journal;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Akka.Persistence.EntityFramework.Journal
{
	public class EntityFrameworkJournal : AsyncWriteProxy
	{
		private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

		protected override void PreStart()
		{
			base.PreStart();
			Self.Tell(new SetStore(Context.ActorOf(Props.Create<EntityFrameworkStore>())));
		}
	}

	internal class EntityFrameworkStore : ActorBase
	{
		private readonly JournalContext _context = new JournalContext();
		private readonly Akka.Serialization.Serialization _serialization = Context.System.Serialization;
		private readonly Akka.Serialization.Serializer _serializer;

		public EntityFrameworkStore()
		{
			_serializer = _serialization.FindSerializerForType(typeof(IPersistentRepresentation));
			/*Receive<AsyncWriteTarget.WriteMessages>(message => Add(message));
			Receive<AsyncWriteTarget.DeleteMessagesTo>(message => Delete(message));
			Receive<AsyncWriteTarget.ReplayMessages>(message => Read(message));
			Receive<AsyncWriteTarget.ReadHighestSequenceNr>(message => GetHighestSequenceNumber(message));
			ReceiveAny(o => Unhandled(o));*/
		}

		protected override bool Receive(object message)
		{
			if (message is AsyncWriteTarget.WriteMessages) Add(message as AsyncWriteTarget.WriteMessages);
			else if (message is AsyncWriteTarget.DeleteMessagesTo) Delete(message as AsyncWriteTarget.DeleteMessagesTo);
			else if (message is AsyncWriteTarget.ReplayMessages) Read(message as AsyncWriteTarget.ReplayMessages);
			else if (message is AsyncWriteTarget.ReadHighestSequenceNr) GetHighestSequenceNumber(message as AsyncWriteTarget.ReadHighestSequenceNr);
			else return false;
			return true;
		}

		private void GetHighestSequenceNumber(AsyncWriteTarget.ReadHighestSequenceNr rhsn)
		{
			var max = from message in _context.Messages
					  where message.PersistentId == rhsn.PersistenceId
					  orderby message.SequenceNr descending
					  select message;

			var highest = max.FirstOrDefault();

			Sender.Tell(highest != null ? highest.SequenceNr : 0L);
		}

		private void Read(AsyncWriteTarget.ReplayMessages replay)
		{
			var list = from message in _context.Messages
					   where message.PersistentId == replay.PersistenceId
					   && message.SequenceNr >= replay.FromSequenceNr
					   && message.SequenceNr <= replay.ToSequenceNr
					   orderby message.SequenceNr
					   select message;

			foreach (var persistent in list.Take(replay.Max >= int.MaxValue ? int.MaxValue : (int)replay.Max))
			{
				var p = PersistentFromByteBuffer(persistent.Payload);
                Sender.Tell(p);
			}
			Sender.Tell(AsyncWriteTarget.ReplaySuccess.Instance);
		}

		private void Delete(AsyncWriteTarget.DeleteMessagesTo deleteCommand)
		{
			if (deleteCommand.IsPermanent) DeletePermanent(deleteCommand);
			DeleteLogical(deleteCommand);
		}

		private void DeleteLogical(AsyncWriteTarget.DeleteMessagesTo deleteCommand)
		{
			var list = from message in _context.Messages
					   where message.PersistentId == deleteCommand.PersistenceId
					   && message.SequenceNr <= deleteCommand.ToSequenceNr
					   orderby message.SequenceNr
					   select message;

			foreach (var persistent in list)
			{
				persistent.Deleted = true;
			}
			_context.SaveChanges();
		}

		private void DeletePermanent(AsyncWriteTarget.DeleteMessagesTo deleteCommand)
		{
			var list = from message in _context.Messages
					   where message.PersistentId == deleteCommand.PersistenceId
					   && message.SequenceNr <= deleteCommand.ToSequenceNr
					   orderby message.SequenceNr
					   select message;

			_context.Messages.RemoveRange(list);

			_context.SaveChanges();
		}

		private void Add(AsyncWriteTarget.WriteMessages writeMessages)
		{
			foreach (var persistent in writeMessages.Messages)
			{
				var message = new Message();
				message.PersistentId = persistent.PersistenceId;
				message.SequenceNr = persistent.SequenceNr;
				message.Payload = PersistentToByteBuffer(persistent);
				message.Deleted = false;
				_context.Messages.Add(message);
			}
			_context.SaveChanges();
			Sender.Tell(new object());
		}

		private Byte[] PersistentToByteBuffer(IPersistentRepresentation p)
		{
			return _serializer.ToBinary(p);
		}

		private IPersistentRepresentation PersistentFromByteBuffer(Byte[] bytes)
		{

			return (IPersistentRepresentation)_serializer.FromBinary(bytes, typeof(IPersistentRepresentation));
		}

		protected override void PostStop()
		{
			base.PostStop();
			_context.Dispose();
		}
	}
}
