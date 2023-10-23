namespace QuickType
{

    public partial class ImportList
    {
        public string name { get; set; }
        public List<Field> fields { get; set; }
    }

    public partial class Field
    {
        public string name { get; set; }
        public string[] value { get; set; }
    }

}
