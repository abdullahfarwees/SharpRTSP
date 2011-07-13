﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NSubstitute;
using NUnit.Framework;
using Rtsp.Messages;
using System.IO;

namespace Rtsp.Tests
{
    [TestFixture]
    public class RtspListenerTest
    {
        IRtspTransport _mockTransport;
        bool _connected = true;
        List<RtspChunk> _receivedMessage;
        List<RtspChunk> _receivedData;

        void MessageReceived(object sender, RtspChunkEventArgs e)
        {
            _receivedMessage.Add(e.Message);
        }

        void DataReceived(object sender, RtspChunkEventArgs e)
        {
            _receivedData.Add(e.Message);
        }

        [SetUp]
        public void Init()
        {
            // Setup a mock
            _mockTransport = Substitute.For<IRtspTransport>();
            _connected = true;
            _mockTransport.Connected.Returns(x => { return _connected; });
            _mockTransport.When(x => x.Close()).Do(x => { _connected = false; });
            _mockTransport.When(x => x.ReConnect()).Do(x => { _connected = true; });

            _receivedData = new List<RtspChunk>();
            _receivedMessage = new List<RtspChunk>();
        }

        [Test]
        public void ReceiveOptionsMessage()
        {
            string message = string.Empty;
            message += "OPTIONS * RTSP/1.0\n";
            message += "CSeq: 1\n";
            message += "Require: implicit-play\n";
            message += "Proxy-Require: gzipped-messages\n";
            message += "\n";
            MemoryStream stream = new MemoryStream(ASCIIEncoding.UTF8.GetBytes(message));
            _mockTransport.GetStream().Returns(stream);

            // Setup test object.
            RtspListener testedListener = new RtspListener(_mockTransport);
            testedListener.MessageReceived += new EventHandler<RtspChunkEventArgs>(MessageReceived);
            testedListener.DataReceived += new EventHandler<RtspChunkEventArgs>(DataReceived);

            // Run
            testedListener.Start();
            System.Threading.Thread.Sleep(100);
            testedListener.Stop();

            // Check the transport was closed.
            _mockTransport.Received().Close();
            //Check the message recevied
            Assert.AreEqual(1, _receivedMessage.Count);
            RtspChunk theMessage = _receivedMessage[0];
            Assert.IsInstanceOf<RtspRequest>(theMessage);
            Assert.AreEqual(0, theMessage.Data.Length);
            Assert.AreSame(testedListener, theMessage.SourcePort);

            RtspRequest theRequest = theMessage as RtspRequest;
            Assert.AreEqual(RtspRequest.RequestType.OPTIONS, theRequest.RequestTyped);
            Assert.AreEqual(3, theRequest.Headers.Count);
            Assert.AreEqual(1, theRequest.CSeq);
            Assert.Contains("Require", theRequest.Headers.Keys);
            Assert.Contains("Proxy-Require", theRequest.Headers.Keys);
            Assert.AreEqual(null, theRequest.RtspUri);

            Assert.AreEqual(0, _receivedData.Count);
        }


        [Test]
        public void ReceivePlayMessage()
        {
            string message = string.Empty;
            message += "PLAY rtsp://audio.example.com/audio RTSP/1.0\r\n";
            message += "CSeq: 835\r\n";
            message += "\r\n";
            MemoryStream stream = new MemoryStream(ASCIIEncoding.UTF8.GetBytes(message));
            _mockTransport.GetStream().Returns(stream);

            // Setup test object.
            RtspListener testedListener = new RtspListener(_mockTransport);
            testedListener.MessageReceived += new EventHandler<RtspChunkEventArgs>(MessageReceived);
            testedListener.DataReceived += new EventHandler<RtspChunkEventArgs>(DataReceived);

            // Run
            testedListener.Start();
            System.Threading.Thread.Sleep(100);
            testedListener.Stop();

            // Check the transport was closed.
            _mockTransport.Received().Close();
            //Check the message recevied
            Assert.AreEqual(1, _receivedMessage.Count);
            RtspChunk theMessage = _receivedMessage[0];
            Assert.IsInstanceOf<RtspRequest>(theMessage);
            Assert.AreEqual(0, theMessage.Data.Length);
            Assert.AreSame(testedListener, theMessage.SourcePort);

            RtspRequest theRequest = theMessage as RtspRequest;
            Assert.AreEqual(RtspRequest.RequestType.PLAY, theRequest.RequestTyped);
            Assert.AreEqual(1, theRequest.Headers.Count);
            Assert.AreEqual(835, theRequest.CSeq);
            Assert.AreEqual("Rtsp://audio.example.com/audio", theRequest.RtspUri.ToString());

            Assert.AreEqual(0, _receivedData.Count);
        }

        [Test]
        public void ReceiveResponseMessage()
        {
            string message = string.Empty;
            message += "RTSP/1.0 551 Option not supported\n";
            message += "CSeq: 302\n";
            message += "Unsupported: funky-feature\n";
            message += "\r\n";
            MemoryStream stream = new MemoryStream(ASCIIEncoding.UTF8.GetBytes(message));
            _mockTransport.GetStream().Returns(stream);

            // Setup test object.
            RtspListener testedListener = new RtspListener(_mockTransport);
            testedListener.MessageReceived += new EventHandler<RtspChunkEventArgs>(MessageReceived);
            testedListener.DataReceived += new EventHandler<RtspChunkEventArgs>(DataReceived);

            // Run
            testedListener.Start();
            System.Threading.Thread.Sleep(100);
            testedListener.Stop();

            // Check the transport was closed.
            _mockTransport.Received().Close();
            //Check the message recevied
            Assert.AreEqual(1, _receivedMessage.Count);
            RtspChunk theMessage = _receivedMessage[0];
            Assert.IsInstanceOf<RtspResponse>(theMessage);
            Assert.AreEqual(0, theMessage.Data.Length);
            Assert.AreSame(testedListener, theMessage.SourcePort);

            RtspResponse theResponse = theMessage as RtspResponse;
            Assert.AreEqual(551, theResponse.ReturnCode);
            Assert.AreEqual("Option not supported", theResponse.ReturnMessage);
            Assert.AreEqual(2, theResponse.Headers.Count);
            Assert.AreEqual(302, theResponse.CSeq);

            Assert.AreEqual(0, _receivedData.Count);
        }


        [Test]
        public void ReceiveData()
        {
            Random rnd = new Random();
            byte[] data = new byte[0x0234];
            rnd.NextBytes(data);

            byte[] buffer = new byte[data.Length + 4];
            buffer[0] = 0x24; // $
            buffer[1] = 11;
            buffer[2] = 0x02;
            buffer[3] = 0x34;
            Buffer.BlockCopy(data, 0, buffer, 4, data.Length);

            MemoryStream stream = new MemoryStream(buffer);
            _mockTransport.GetStream().Returns(stream);

            // Setup test object.
            RtspListener testedListener = new RtspListener(_mockTransport);
            testedListener.MessageReceived += new EventHandler<RtspChunkEventArgs>(MessageReceived);
            testedListener.DataReceived += new EventHandler<RtspChunkEventArgs>(DataReceived);

            // Run
            testedListener.Start();
            System.Threading.Thread.Sleep(500);
            testedListener.Stop();

            // Check the transport was closed.
            _mockTransport.Received().Close();
            //Check the message recevied
            Assert.AreEqual(0, _receivedMessage.Count);
            Assert.AreEqual(1, _receivedData.Count);
            Assert.IsInstanceOf<RtspData>(_receivedData[0]);
            RtspData dataMessage = _receivedData[0] as RtspData;

            Assert.AreEqual(11, dataMessage.Channel);
            Assert.AreSame(testedListener, dataMessage.SourcePort);
            Assert.AreEqual(data, dataMessage.Data);


        }

        [Test]
        public void ReceiveNoMessage()
        {
            string message = string.Empty;
            MemoryStream stream = new MemoryStream(ASCIIEncoding.UTF8.GetBytes(message));
            _mockTransport.GetStream().Returns(stream);

            // Setup test object.
            RtspListener testedListener = new RtspListener(_mockTransport);
            testedListener.MessageReceived += new EventHandler<RtspChunkEventArgs>(MessageReceived);
            testedListener.DataReceived += new EventHandler<RtspChunkEventArgs>(DataReceived);

            // Run
            testedListener.Start();
            System.Threading.Thread.Sleep(100);
            testedListener.Stop();

            // Check the transport was closed.
            _mockTransport.Received().Close();
            Assert.AreEqual(0, _receivedMessage.Count);
            Assert.AreEqual(0, _receivedData.Count);
        }


    }
}