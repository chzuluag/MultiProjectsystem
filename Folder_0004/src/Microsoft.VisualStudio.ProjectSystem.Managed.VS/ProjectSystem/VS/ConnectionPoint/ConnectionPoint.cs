// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.VisualStudio.ProjectSystem.VS.ConnectionPoint
{
    /// <summary>
    /// This implementation is a copy from CPS
    /// </summary>
    internal class ConnectionPoint<TSinkType> : IConnectionPoint
            where TSinkType : class
    {
        private readonly Dictionary<uint, TSinkType> _sinks = new Dictionary<uint, TSinkType>();
        private readonly ConnectionPointContainer _container;
        private readonly IEventSource<TSinkType> _source;

        private uint _nextCookie;

        internal ConnectionPoint(ConnectionPointContainer container, IEventSource<TSinkType> source)
        {
            Requires.NotNull(container, nameof(container));
            Requires.NotNull(source, nameof(source));

            _container = container;
            _source = source;
            _nextCookie = 1;
        }

        public void Advise(object pUnkSink, out uint pdwCookie)
        {
            if (pUnkSink is TSinkType sink)
            {
                _sinks.Add(_nextCookie, sink);
                pdwCookie = _nextCookie;
                _source.OnSinkAdded(sink);
                _nextCookie += 1;
                return;
            }

            Marshal.ThrowExceptionForHR(HResult.NoInterface);
            pdwCookie = default;
        }

        public void EnumConnections(out IEnumConnections ppEnum)
        {
            throw new NotImplementedException();
        }

        public void GetConnectionInterface(out Guid pIID)
        {
            pIID = typeof(TSinkType).GUID;
        }

        public void GetConnectionPointContainer(out IConnectionPointContainer ppCPC)
        {
            ppCPC = _container;
        }

        public void Unadvise(uint dwCookie)
        {
            // This will throw if the cookie is not in the list.
            TSinkType sink = _sinks[dwCookie];
            _sinks.Remove(dwCookie);
            _source.OnSinkRemoved(sink);
        }
    }
}
