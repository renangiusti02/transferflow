using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TransferFlow.Infrastructure.Messaging.Inbox;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        builder.HasKey(processedMessage => processedMessage.MessageId);

        builder.Property(processedMessage => processedMessage.MessageId)
            .HasColumnName("message_id")
            .ValueGeneratedNever();

        builder.Property(processedMessage => processedMessage.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamp with time zone");
    }
}
