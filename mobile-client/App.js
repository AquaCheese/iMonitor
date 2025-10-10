import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Dimensions,
  TouchableOpacity,
  Alert,
  StatusBar,
  Image,
  PanResponder,
  Animated
} from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Orientation from 'react-native-orientation-locker';
import KeepAwake from 'react-native-keep-awake';

const { width: screenWidth, height: screenHeight } = Dimensions.get('window');

const App = () => {
  const [isConnected, setIsConnected] = useState(false);
  const [isStreaming, setIsStreaming] = useState(false);
  const [serverURL, setServerURL] = useState('');
  const [displayFrame, setDisplayFrame] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState('disconnected');
  const [showControls, setShowControls] = useState(true);
  
  const wsRef = useRef(null);
  const controlsTimeoutRef = useRef(null);
  const scaleAnim = useRef(new Animated.Value(1)).current;

  useEffect(() => {
    initializeApp();
    return () => {
      if (wsRef.current) {
        wsRef.current.close();
      }
      Orientation.unlockAllOrientations();
    };
  }, []);

  const initializeApp = async () => {
    // Lock to landscape for better display experience
    Orientation.lockToLandscape();
    
    // Keep screen awake during streaming
    KeepAwake.activate();

    // Load saved server URL
    try {
      const savedURL = await AsyncStorage.getItem('serverURL');
      if (savedURL) {
        setServerURL(savedURL);
      }
    } catch (error) {
      console.log('Failed to load saved server URL:', error);
    }
  };

  const connectToServer = async (url) => {
    if (!url) {
      Alert.alert('Error', 'Please enter a server URL');
      return;
    }

    setConnectionStatus('connecting');
    
    try {
      // Save server URL
      await AsyncStorage.setItem('serverURL', url);
      setServerURL(url);

      // Create WebSocket connection
      const wsURL = url.replace('http://', 'ws://').replace('https://', 'wss://') + ':8081';
      wsRef.current = new WebSocket(wsURL);

      wsRef.current.onopen = () => {
        console.log('WebSocket connected');
        setIsConnected(true);
        setConnectionStatus('connected');
        requestScreenStream();
      };

      wsRef.current.onmessage = (event) => {
        handleMessage(JSON.parse(event.data));
      };

      wsRef.current.onclose = () => {
        console.log('WebSocket disconnected');
        setIsConnected(false);
        setIsStreaming(false);
        setConnectionStatus('disconnected');
        setDisplayFrame(null);
      };

      wsRef.current.onerror = (error) => {
        console.error('WebSocket error:', error);
        Alert.alert('Connection Error', 'Failed to connect to desktop server');
        setConnectionStatus('disconnected');
      };

    } catch (error) {
      console.error('Connection error:', error);
      Alert.alert('Error', 'Failed to connect: ' + error.message);
      setConnectionStatus('disconnected');
    }
  };

  const disconnect = () => {
    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }
    setIsConnected(false);
    setIsStreaming(false);
    setDisplayFrame(null);
    KeepAwake.deactivate();
  };

  const handleMessage = (message) => {
    switch (message.type) {
      case 'WELCOME':
        console.log('Welcome message received');
        break;
      case 'SCREEN_FRAME':
        handleScreenFrame(message.data);
        break;
      default:
        console.log('Unknown message type:', message.type);
    }
  };

  const handleScreenFrame = (frameData) => {
    if (frameData.image) {
      const imageUri = `data:image/${frameData.format};base64,${frameData.image}`;
      setDisplayFrame(imageUri);
      
      if (!isStreaming) {
        setIsStreaming(true);
        hideControlsAfterDelay();
      }
    }
  };

  const requestScreenStream = () => {
    if (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN) return;

    const message = {
      type: 'REQUEST_SCREEN_STREAM',
      data: {
        clientType: 'mobile',
        capabilities: {
          touch: true,
          mouse: true,
          keyboard: false
        }
      }
    };

    wsRef.current.send(JSON.stringify(message));
  };

  const sendInputEvent = (event) => {
    if (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN) return;

    const message = {
      type: 'INPUT_EVENT',
      data: {
        ...event,
        timestamp: Date.now()
      }
    };

    wsRef.current.send(JSON.stringify(message));
  };

  const hideControlsAfterDelay = () => {
    if (controlsTimeoutRef.current) {
      clearTimeout(controlsTimeoutRef.current);
    }
    
    controlsTimeoutRef.current = setTimeout(() => {
      setShowControls(false);
    }, 3000);
  };

  const showControlsTemporarily = () => {
    setShowControls(true);
    hideControlsAfterDelay();
  };

  // Pan responder for touch handling
  const panResponder = PanResponder.create({
    onStartShouldSetPanResponder: () => true,
    onMoveShouldSetPanResponder: () => true,
    
    onPanResponderGrant: (evt) => {
      const { locationX, locationY } = evt.nativeEvent;
      const x = locationX / screenWidth;
      const y = locationY / screenHeight;
      
      sendInputEvent({
        eventType: 'touch',
        action: 'start',
        x,
        y
      });

      // Visual feedback
      Animated.sequence([
        Animated.timing(scaleAnim, {
          toValue: 0.98,
          duration: 100,
          useNativeDriver: true,
        }),
        Animated.timing(scaleAnim, {
          toValue: 1,
          duration: 100,
          useNativeDriver: true,
        }),
      ]).start();
    },

    onPanResponderMove: (evt) => {
      const { locationX, locationY } = evt.nativeEvent;
      const x = locationX / screenWidth;
      const y = locationY / screenHeight;
      
      sendInputEvent({
        eventType: 'touch',
        action: 'move',
        x,
        y
      });
    },

    onPanResponderRelease: (evt) => {
      const { locationX, locationY } = evt.nativeEvent;
      const x = locationX / screenWidth;
      const y = locationY / screenHeight;
      
      sendInputEvent({
        eventType: 'touch',
        action: 'end',
        x,
        y
      });
    },
  });

  const renderConnectionScreen = () => (
    <View style={styles.connectionScreen}>
      <Text style={styles.title}>iMonitor</Text>
      <Text style={styles.subtitle}>Extended Display</Text>
      
      <View style={styles.serverInputContainer}>
        <Text style={styles.inputLabel}>Desktop Server IP:</Text>
        <Text style={styles.serverURLText}>{serverURL || 'No server configured'}</Text>
      </View>

      <TouchableOpacity 
        style={[styles.connectButton, connectionStatus === 'connecting' && styles.connectButtonDisabled]}
        onPress={() => {
          Alert.prompt(
            'Server URL',
            'Enter your desktop server IP address:',
            [
              { text: 'Cancel', style: 'cancel' },
              { 
                text: 'Connect', 
                onPress: (url) => {
                  if (url) {
                    connectToServer(`http://${url}`);
                  }
                }
              }
            ],
            'plain-text',
            serverURL ? serverURL.replace('http://', '') : ''
          );
        }}
        disabled={connectionStatus === 'connecting'}
      >
        <Text style={styles.connectButtonText}>
          {connectionStatus === 'connecting' ? 'Connecting...' : 'Connect to Desktop'}
        </Text>
      </TouchableOpacity>

      <Text style={styles.helpText}>
        Make sure your mobile device and desktop are on the same network
      </Text>
    </View>
  );

  const renderDisplayScreen = () => (
    <Animated.View 
      style={[styles.displayContainer, { transform: [{ scale: scaleAnim }] }]}
      {...panResponder.panHandlers}
    >
      {displayFrame ? (
        <Image 
          source={{ uri: displayFrame }}
          style={styles.displayImage}
          resizeMode="contain"
        />
      ) : (
        <View style={styles.loadingContainer}>
          <Text style={styles.loadingText}>Waiting for display stream...</Text>
        </View>
      )}

      {/* Status indicator */}
      <View style={[styles.statusIndicator, { 
        backgroundColor: isStreaming ? '#4CAF50' : '#FF9800' 
      }]} />

      {/* Controls */}
      {showControls && (
        <View style={styles.controls}>
          <TouchableOpacity 
            style={styles.controlButton}
            onPress={() => showControlsTemporarily()}
          >
            <Text style={styles.controlButtonText}>👁</Text>
          </TouchableOpacity>
          
          <TouchableOpacity 
            style={styles.controlButton}
            onPress={disconnect}
          >
            <Text style={styles.controlButtonText}>✕</Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Tap to show controls */}
      {!showControls && (
        <TouchableOpacity 
          style={styles.tapToShowControls}
          onPress={showControlsTemporarily}
          activeOpacity={0.1}
        />
      )}
    </Animated.View>
  );

  return (
    <View style={styles.container}>
      <StatusBar hidden={isStreaming} />
      
      {isConnected ? renderDisplayScreen() : renderConnectionScreen()}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000',
  },
  connectionScreen: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
    background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
    backgroundColor: '#667eea',
  },
  title: {
    fontSize: 48,
    fontWeight: 'bold',
    color: '#fff',
    marginBottom: 10,
  },
  subtitle: {
    fontSize: 18,
    color: '#fff',
    opacity: 0.8,
    marginBottom: 40,
  },
  serverInputContainer: {
    width: '100%',
    marginBottom: 30,
  },
  inputLabel: {
    color: '#fff',
    fontSize: 16,
    marginBottom: 10,
  },
  serverURLText: {
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    color: '#fff',
    padding: 15,
    borderRadius: 8,
    fontSize: 16,
    textAlign: 'center',
  },
  connectButton: {
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    paddingHorizontal: 40,
    paddingVertical: 15,
    borderRadius: 25,
    borderWidth: 2,
    borderColor: 'rgba(255, 255, 255, 0.3)',
  },
  connectButtonDisabled: {
    opacity: 0.5,
  },
  connectButtonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: 'bold',
  },
  helpText: {
    color: '#fff',
    fontSize: 14,
    opacity: 0.7,
    textAlign: 'center',
    marginTop: 30,
    maxWidth: 300,
  },
  displayContainer: {
    flex: 1,
    backgroundColor: '#000',
  },
  displayImage: {
    width: '100%',
    height: '100%',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    color: '#fff',
    fontSize: 18,
  },
  statusIndicator: {
    position: 'absolute',
    top: 20,
    right: 20,
    width: 12,
    height: 12,
    borderRadius: 6,
    zIndex: 1000,
  },
  controls: {
    position: 'absolute',
    bottom: 30,
    left: 0,
    right: 0,
    flexDirection: 'row',
    justifyContent: 'center',
    gap: 20,
    zIndex: 1000,
  },
  controlButton: {
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    width: 50,
    height: 50,
    borderRadius: 25,
    justifyContent: 'center',
    alignItems: 'center',
  },
  controlButtonText: {
    color: '#fff',
    fontSize: 20,
  },
  tapToShowControls: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    zIndex: 999,
  },
});

export default App;