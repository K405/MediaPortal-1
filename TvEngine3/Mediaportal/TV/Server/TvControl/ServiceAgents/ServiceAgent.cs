using System;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace Mediaportal.TV.Server.TVControl.ServiceAgents
{
  public abstract class ServiceAgent<T> : IDisposable
  {
    protected T _channel;

    protected ServiceAgent()
    {
    }

    protected ServiceAgent(string hostname)
    {
      if (!String.IsNullOrEmpty(hostname))
      {
        BasicHttpBinding binding = ServiceHelper.GetHttpBinding();
        var endpoint = new EndpointAddress(ServiceHelper.GetEndpointURL(typeof (T), hostname));

        var channelFactory = new ChannelFactory<T>(binding, endpoint);

        foreach (OperationDescription op in channelFactory.Endpoint.Contract.Operations)
        {
          var dataContractBehavior = op.Behaviors.Find<DataContractSerializerOperationBehavior>();
          if (dataContractBehavior != null)
          {
            dataContractBehavior.MaxItemsInObjectGraph = int.MaxValue;
          }
        }
        _channel = channelFactory.CreateChannel();

        ((IClientChannel) _channel).Faulted += ServiceAgent_Faulted;
        ((IClientChannel) _channel).Closed += ServiceAgent_Closed;
      }
    }

    #region IDisposable Members

    public virtual void Dispose()
    {
      var clientChannel = _channel as IClientChannel;
      if (clientChannel == null)
      {
        return;
      }

      clientChannel.Faulted -= ServiceAgent_Faulted;
      clientChannel.Closed -= ServiceAgent_Closed;

      try
      {
        if (clientChannel.State != CommunicationState.Faulted)
        {
          // we need this timeout, otherwise the call to 'Close' will block until any ongoing parallel WCF calls are still active          
          // so instead of having to wait for the default timeout of 1min, we instead wait 1 sec, before giving up and instead calls 'Abort'
          var timeout = new TimeSpan(0, 0, 0, 1);
          clientChannel.Close(timeout);
        }
        else
        {
          clientChannel.Abort();
        }
      }
      catch (CommunicationException ce)
      {
        clientChannel.Abort();
      }
      catch (TimeoutException toe)
      {
        clientChannel.Abort();
      }
      catch (Exception e)
      {
        clientChannel.Abort();
      }
      finally
      {
        clientChannel.Dispose();
        clientChannel = null;
      }
    }

    #endregion

    public event EventHandler ServiceAgentRemoved;
    public event EventHandler ServiceAgentFaulted;
    public event EventHandler ServiceAgentClosed;

    private void ServiceAgent_Closed(object sender, EventArgs e)
    {
      if (ServiceAgentClosed != null)
      {
        ServiceAgentClosed(sender, e);
      }
    }

    private void ServiceAgent_Faulted(object sender, EventArgs e)
    {
      if (ServiceAgentFaulted != null)
      {
        ServiceAgentFaulted(sender, e);
      }
    }
  }
}