namespace MarcusRunge.Base
{
    /// <summary>
    /// Provides a base class for bindable entities.
    /// </summary>
    public abstract class BindableEntityBase : BindableBase
    {
        private int _id;

        protected BindableEntityBase()
        {
        }

        protected BindableEntityBase(int id) : base() => Id = id;

        /// <summary>
        /// The entity's identity
        /// </summary>
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        /// <summary>
        /// The entity's timestamp for optimistic concurrency
        /// </summary>
        public byte[]? RowVersion { get; set; }
    }
}