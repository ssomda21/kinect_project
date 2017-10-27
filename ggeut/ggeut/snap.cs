using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;


namespace ggeut
{
    class snap
    {
        #region Member Variables
        public List<List<short>> datas;

        public int width;
        public int height;

        private int index;
        private int id;

        public List<Point> skels;

        private static snap instance = null;

        private readonly object lockObject = new object();
        #endregion Member Variables

        #region Constructor
        private snap(int _width, int _height)
        {
            width = _width;
            height = _height;

            index = 0;
            id = 0;

            datas = new List<List<short>>();
            skels = new List<Point>();
        }
        #endregion Constructor

        #region Methods
        public void addData(short[] data)
        {
            datas.Add(data.OfType<short>().ToList());

            addIndex();
        }

        private void addIndex()
        {
            lock (lockObject)
            {
                index += 1;
            }
        }

        public int getIndex()
        {
            lock (lockObject)
            {
                return index;
            }
        }

        public static snap getInstance(int _width, int _height)
        {
            if (instance == null)
            {
                instance = new snap(_width, _height);
            }

            return instance;
        }

        public void Save()
        {
            Stream saveStream = File.Open(id + ".data", FileMode.Create, FileAccess.Write);

            BinaryFormatter bf = new BinaryFormatter();

            bf.Serialize(saveStream, this);

            saveStream.Close();
            saveStream = null;
            bf = null;
        }
        #endregion Methods
    }
}
