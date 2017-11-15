namespace Synthesis.GuestService.Utilities.Objects
{
    public class PagingMetaData
    {
        /// <summary>
        /// The total count of all records the current user can access
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// The number of records that macth the searched criteria
        /// </summary>
        public int CurrentCount { get; set; }

        /// <summary>
        /// The page that is requested. The first page is 1.
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// The search criteria entered by the user in the search field
        /// </summary>
        public string SearchFilter { get; set; }

        /// <summary>
        /// The name of the column to sort
        /// </summary>
        public string SortColumn { get; set; }

        /// <summary>
        /// The sort order
        /// </summary>
        public DataSortOrder SortOrder { get; set; }
    }
}