namespace MarcusRunge.Base
{
    /// <summary>
    /// Provides a base class for bindable entities.
    /// </summary>
    public abstract class BindableEntityBase : BindableBase
    {
        private int _id;

        /// <summary>
        /// Initializes a new instance of the <see cref="BindableEntityBase"/> class.
        /// </summary>
        protected BindableEntityBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BindableEntityBase"/> class.
        /// </summary>
        /// <param name="id">The entity's identity.</param>
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