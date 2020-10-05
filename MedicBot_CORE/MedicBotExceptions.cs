using System;
using System.Runtime.Serialization;

namespace MedicBot
{
    // Exception messages can be used as a way to send failed search parameters to the handler event so they can be shown to the user with the error message.
    // but it's not really necessarry as most error messages come directly after a user's message

    class AudioEntryNotFoundException : Exception
    {
        public AudioEntryNotFoundException()
        {
        }

        public AudioEntryNotFoundException(string message) : base(message)
        {
        }

        public AudioEntryNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AudioEntryNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    class NoResultsFoundException : Exception
    {
        public NoResultsFoundException()
        {
        }

        public NoResultsFoundException(string message) : base(message)
        {
        }

        public NoResultsFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NoResultsFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    class CollectionNotFoundException : Exception
    {
        public CollectionNotFoundException()
        {
        }

        public CollectionNotFoundException(string message) : base(message)
        {
        }

        public CollectionNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CollectionNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
