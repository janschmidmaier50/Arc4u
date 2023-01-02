﻿using System;
using System.Threading.Tasks;
using Arc4u.Network.Pooling;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace Arc4u.Standard.Ftp;

/// <summary>
///     Factory to create <see cref="PoolableItem" /> in form of an SftpCLient
/// </summary>
public class SftpClientFactory : IClientFactory<SftpClientFacade>
{
    private readonly ILogger<SftpClientFacade> _clientLogger;
    private readonly Func<SftpClient> _sftpFactory;

    /// <summary>
    ///     Instantiates a new factory
    /// </summary>
    /// <param name="sftpFactory">factory to create new poolable sftp clients</param>
    /// <param name="clientLogger">Logger for the poolable sftp clients</param>
    public SftpClientFactory(Func<SftpClient> sftpFactory, ILogger<SftpClientFacade> clientLogger)
    {
        _sftpFactory = sftpFactory;
        _clientLogger = clientLogger;
    }

    /// <summary>
    ///     Builds a new poolable sftp client, by using the provided configuration of the ctor
    /// </summary>
    /// <returns>poolable sftp client</returns>
    /// <remarks>
    ///     The created internally created client is using a BasicAuth (username & password) and has a keep alive interval
    ///     activated. The provided client is already connected to the sftp server
    /// </remarks>
    public SftpClientFacade CreateClient(Func<SftpClientFacade, Task> releaseFunc)
    {
        return new SftpClientFacade(_sftpFactory(), releaseFunc, _clientLogger);
    }
}