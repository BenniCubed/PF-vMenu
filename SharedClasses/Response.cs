namespace vMenuShared
{
    public struct Response<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
    }
}
