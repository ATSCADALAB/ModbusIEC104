using System;
using System.Collections.Generic;

namespace IEC104
{
    /// <summary>
    /// Base class cho tất cả các loại address
    /// </summary>
    public abstract class Address : ICloneable, IComparable<Address>
    {
        #region PROPERTIES
        /// <summary>Địa chỉ đầy đủ dạng string</summary>
        public abstract string FullAddress { get; }
        #endregion

        #region ABSTRACT METHODS
        /// <summary>
        /// Parse địa chỉ từ string
        /// </summary>
        /// <param name="address">Chuỗi địa chỉ</param>
        public abstract void ParseAddress(string address);

        /// <summary>
        /// Kiểm tra địa chỉ có hợp lệ không
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public abstract bool IsValid();

        /// <summary>
        /// Clone địa chỉ
        /// </summary>
        /// <returns>Bản copy của địa chỉ</returns>
        public abstract object Clone();
        #endregion

        #region VIRTUAL METHODS
        /// <summary>
        /// So sánh với địa chỉ khác
        /// </summary>
        /// <param name="other">Địa chỉ khác</param>
        /// <returns>Kết quả so sánh</returns>
        public virtual int CompareTo(Address other)
        {
            if (other == null) return 1;
            return string.Compare(FullAddress, other.FullAddress, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return FullAddress;
        }
        #endregion
    }

    /// <summary>
    /// Base class cho Device Settings
    /// </summary>
    public abstract class DeviceSettings
    {
        #region PROPERTIES
        /// <summary>Địa chỉ IP của thiết bị</summary>
        public string IpAddress { get; set; } = "127.0.0.1";

        /// <summary>Cổng kết nối</summary>
        public ushort Port { get; set; } = 502;

        /// <summary>Khoảng thời gian đọc dữ liệu (ms)</summary>
        public int ReadInterval { get; set; } = 1000;

        /// <summary>Timeout cho kết nối (ms)</summary>
        public int ConnectionTimeout { get; set; } = 10000;

        /// <summary>Timeout cho đọc dữ liệu (ms)</summary>
        public int ReadTimeout { get; set; } = 5000;

        /// <summary>Client ID duy nhất</summary>
        public abstract string ClientID { get; }

        /// <summary>Danh sách block settings</summary>
        public List<BlockSettings> BlockSettings { get; set; } = new List<BlockSettings>();
        #endregion

        #region METHODS
        /// <summary>
        /// Validate settings
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public virtual bool IsValid()
        {
            return !string.IsNullOrEmpty(IpAddress) &&
                   Port > 0 &&
                   ReadInterval > 0 &&
                   ConnectionTimeout > 0 &&
                   ReadTimeout > 0;
        }
        #endregion
    }

    /// <summary>
    /// Base class cho Block Settings
    /// </summary>
    public abstract class BlockSettings
    {
        #region PROPERTIES
        /// <summary>Tên block</summary>
        public string Name { get; set; }

        /// <summary>Block có được kích hoạt không</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Khoảng thời gian đọc riêng cho block này (ms)</summary>
        public int ReadInterval { get; set; } = 0; // 0 = sử dụng device read interval

        /// <summary>Địa chỉ bắt đầu</summary>
        public string StartAddress { get; set; }

        /// <summary>Số lượng register/point cần đọc</summary>
        public int Count { get; set; } = 1;

        /// <summary>Block ID duy nhất</summary>
        public abstract string BlockID { get; }
        #endregion

        #region METHODS
        /// <summary>
        /// Validate block settings
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public virtual bool IsValid()
        {
            return !string.IsNullOrEmpty(Name) &&
                   !string.IsNullOrEmpty(StartAddress) &&
                   Count > 0;
        }
        #endregion
    }

    /// <summary>
    /// Quyền truy cập dữ liệu
    /// </summary>
    public enum AccessRight
    {
        /// <summary>Chỉ đọc</summary>
        Read = 1,

        /// <summary>Chỉ ghi</summary>
        Write = 2,

        /// <summary>Đọc và ghi</summary>
        ReadWrite = 3
    }

    /// <summary>
    /// Thứ tự byte
    /// </summary>
    public enum ByteOrder
    {
        /// <summary>Little Endian (Intel)</summary>
        LittleEndian = 0,

        /// <summary>Big Endian (Motorola)</summary>
        BigEndian = 1,

        /// <summary>Little Endian với swap word</summary>
        LittleEndianByteSwap = 2,

        /// <summary>Big Endian với swap word</summary>
        BigEndianByteSwap = 3
    }
}