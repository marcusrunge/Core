using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarcusRunge.Base.EntityFramework
{
    /// <summary>
    /// Provides a base class for entity configurations.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TConfiguration">The type of the configuration.</typeparam>
    public abstract class EntityConfigurationBase<TEntity, TConfiguration> : IEntityTypeConfiguration<TEntity>
        where TEntity : BindableEntityBase
        where TConfiguration : EntityConfigurationBase<TEntity, TConfiguration>, new()
    {
        /// <inheritdoc/>
        public virtual void Configure(EntityTypeBuilder<TEntity> builder)
        {
            builder.
                HasKey(@base => @base.Id);
            builder
                .Property(@base => @base.Id)
                .ValueGeneratedOnAdd();
            builder
                .Property(@base => @base.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate();
        }

        /// <summary>
        /// Creates a new instance of the entity configuration.
        /// </summary>
        /// <returns>The new instance of the entity configuration.</returns>
        public static TConfiguration Create() => new();
    }
}